using Duckov;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CharacterIzuna
{

    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        private CharacterModel characterModel;

        private Movement movement;

        private string[] pathsToHide = new string[]
        {
            "CustomFaceInstance/DuckBody",
            "CustomFaceInstance/Armature/Root/Pelvis/Thigh.L",
            "CustomFaceInstance/Armature/Root/Pelvis/Thigh.R",
            "CustomFaceInstance/Armature/Root/Pelvis/Spine.001/Spine.002/Spine.003/Spine.004/Head",
            "CustomFaceInstance/Armature/Root/Pelvis/Spine.001/Spine.002/Spine.003/Spine.004/UpperArm.L/Wings.L",
            "CustomFaceInstance/Armature/Root/Pelvis/Spine.001/Spine.002/Spine.003/Spine.004/UpperArm.R/Wings.R",
            "CustomFaceInstance/Armature/Root/Pelvis/Spine.001/Spine.002/Spine.003/Spine.004/ArmorSocket",
            "CustomFaceInstance/Armature/Root/Pelvis/Spine.001/Spine.002/Spine.003/Spine.004/BackpackSocket",
            "CustomFaceInstance/Armature/Root/Pelvis/TailSocket"
        };

        private List<GameObject> hideGameObject = new List<GameObject>();

        private AssetBundle loadedBundle;

        private Animator characterAnimator;

        private GameObject loadedObject;

        private GameObject instantedObject;

        private bool isSetModel;

        private bool wasRunning;

        private bool wasMoving;

        private bool wasDashing;

        private InputAction newAction = new InputAction();

        private List<string> soundPath = new List<string>();

        private bool quackEnabled = false;

        private bool isGunActive = true;

        private bool isTriggerInput = false;

        private bool isTriggerDown = false;

        private List<MeshRenderer> mrToHide = new List<MeshRenderer>();


        private void Update()
        {
            if (instantedObject != null) UpdateAnimation();
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.H))
            {
                isGunActive = !isGunActive;
                LoadAllHoldingItemRenderers();
                ReloadHoldingVisual();
            }
        }

        private void ToggleModel(bool value)
        {
            isSetModel = value;
            if (isSetModel) SetModel();
            else RestoreModel();
        }
        private void OnEnable()
        {
            Debug.Log("CharacterIzuna 已启用");
            InitQuackKey();
            StartCoroutine(LoadCharacterBundle());
            LevelManager.OnLevelInitializingCommentChanged += OnCommentChanged;
            GameManager.MainPlayerInput.onControlsChanged += OnControlsChanged;
        }

        private void OnControlsChanged(PlayerInput input)
        {
            Invoke(nameof(InitQuackKey), 0.25f);
        }

        private void OnDisable()
        {
            LevelManager.OnLevelInitializingCommentChanged -= OnCommentChanged;
            CharacterMainControl.Main.OnAttackEvent -= MeleeAnim;
            CharacterMainControl.Main.OnHoldAgentChanged -= HoldingItemChanged;
            CharacterMainControl.Main.OnTriggerInputUpdateEvent -= TriggerEvent;
            CharacterMainControl.Main.OnActionStartEvent -= ActionStart;
            GameManager.MainPlayerInput.onControlsChanged -= OnControlsChanged;
            if (loadedObject != null)
            {
                Destroy(loadedObject);
                loadedObject = null;
            }
            if (loadedBundle != null)
            {
                loadedBundle.Unload(true);
                loadedBundle = null;
            }
            Debug.Log("CharacterIzuna 已禁用");
        }


        private void MeleeAnim(DuckovItemAgent agent)
        {
            if (characterAnimator == null) return;
            characterAnimator.Play("Melee");
        }

        private void InitQuackKey()
        {
            InitSoundFilePath();
            if (soundPath.Count < 1)
            {
                Debug.Log("CharacterIzuna : 声音文件不存在！");
                return;
            }
            InputActionAsset actions = GameManager.MainPlayerInput.actions;
            InputAction quackAction = actions.FindAction("Quack");
            quackAction.Disable();
            newAction = new InputAction();
            newAction.AddBinding(quackAction.controls[0]);
            newAction.performed += PlaySound;
            newAction.Enable();
        }

        private void DisabelQuack()
        {
            if (!quackEnabled) return;
            quackEnabled = false;
            newAction.performed -= PlaySound;
            newAction.Disable();
            InputActionAsset actions = GameManager.MainPlayerInput.actions;
            InputAction quackAction = actions.FindAction("Quack");
            quackAction.Enable();
        }

        private void PlaySound(InputAction.CallbackContext context)
        {
            if (CharacterMainControl.Main == null) return;
            int random = UnityEngine.Random.Range(0, soundPath.Count);
            AudioManager.PostCustomSFX(soundPath[random]);

            AISound sound = new AISound();
            sound.fromCharacter = CharacterMainControl.Main;
            sound.fromObject = base.gameObject;
            sound.pos = CharacterMainControl.Main.characterModel.transform.position;
            sound.fromTeam = 0;
            sound.soundType = SoundTypes.unknowNoise;
            sound.radius = 15f;
            AIMainBrain.MakeSound(sound);
        }

        private void OnCommentChanged(string obj)
        {
            if (obj == "Setting character position...")
            {
                Debug.Log("CharacterIzuna ：准备加载角色模型。");
                ToggleModel(true);
            }
            if (obj == "Starting up...")
            {
                ToggleModel(false);
            }
        }

        private string GetSpace(Transform t)
        {
            string space = "";
            Transform currentParent = t.parent;
            while (currentParent != null) 
            {
                space += "--";
                currentParent = currentParent.parent;
            }
            return space;
        }

        private void OnDestroy()
        {
        }

        private void SetModel()
        {
            LevelManager instance = LevelManager.Instance;
            characterModel = instance.MainCharacter.characterModel;
            movement = characterModel.characterMainControl.movementControl;
            wasRunning = movement.Running;
            wasMoving = movement.Moving;
            wasDashing = characterModel.characterMainControl.Dashing;
            HideDuck();
            InitializeCharacter(loadedObject);
        }

        private void RestoreModel()
        {
            //RestoreDuck();
            //UnloadUnityHoshino();
            //ResetAnimation();
        }

        private void HideDuck()
        {
            Transform transform = characterModel.transform;
            hideGameObject.Clear();
            foreach (string n in pathsToHide)
            {
                Transform target = transform.Find(n);
                if (target != null)
                {
                    hideGameObject.Add(target.gameObject);
                    target.gameObject.SetActive(false);
                }
            }
        }

        private void RestoreDuck()
        {
            foreach (GameObject gameObject in this.hideGameObject)
            {
                gameObject.SetActive(true);
            }
            this.hideGameObject.Clear();
        }

        private void UnloadModel()
        {
            bool flag = instantedObject != null;
            if (flag)
            {
                Destroy(instantedObject);
                instantedObject = null;
            }
            characterAnimator = null;
        }

        private void ResetAnimation()
        {
            wasMoving = false;
            wasRunning = false;
            wasDashing = false;
        }

        private void ReloadHoldingVisual()
        {
            for (int i = 0; i < mrToHide.Count; i++)
            {
                mrToHide[i].enabled = isGunActive;
            }
        }


        private void LoadAllHoldingItemRenderers()
        {
            Transform targetTransform = CharacterMainControl.Main.RightHandSocket;
            mrToHide.Clear();
            LoadRenderers(targetTransform);
        }
        private void LoadRenderers(Transform target)
        {
            MeshRenderer mr = target.GetComponent<MeshRenderer>();
            if (mr != null) mrToHide.Add(mr);
            if (target.childCount == 0)
            {
                return;
            }
            for (int i = 0; i < target.childCount; i++)
            {
                LoadRenderers(target.GetChild(i));
            }
        }


        private void UpdateAnimation()
        {
            if (characterAnimator == null) return;
            bool moving = movement.Moving;
            bool running = movement.Running;
            bool dashing = characterModel.characterMainControl.Dashing;

            if (dashing && dashing != wasDashing) characterAnimator.Play("Dash");

            //地堡奔跑速度恒定为7.5582
            //角色当前移动速度为 CharacterMainControl.Main.Velocity.magnitude
            //角色当前最大奔跑速度为 CharacterMainControl.Main.CharacterRunSpeed
            //speed参数应夹在0.1到1.5之间

            characterAnimator.SetBool("Running", true);
            float speedProgress = CharacterMainControl.Main.Velocity.magnitude / 7f;
            float speedParam = Mathf.Lerp(0.01f, 1.3f, speedProgress);

            if (moving || running) characterAnimator.SetFloat("MovingSpeed", speedParam);
            else characterAnimator.SetFloat("MovingSpeed", 0);


            wasMoving = moving;
            wasRunning = running;
            wasDashing = dashing;

            bool aiming = CharacterMainControl.Main.IsInAdsInput || isTriggerInput;
            characterAnimator.SetBool("Aiming", aiming);

            characterAnimator.SetFloat("Attack", isTriggerDown ? 0.99f : 0f);
        }

        IEnumerator LoadCharacterBundle()
        {
            if (loadedBundle != null) 
            {
                Debug.Log("CharacterIzuna : 已经加载过资源包了");
                yield break;
            }
            string bundlePath = GetDllDirectory() + "/izuna";
            AssetBundleCreateRequest request = AssetBundle.LoadFromFileAsync(bundlePath);
            if (request == null) yield return null;
            else yield return request;
            loadedBundle = request.assetBundle;
            if (loadedBundle == null)
            {
                Debug.LogError("CharacterIzuna : 无法加载资源包!");
                yield break;
            }
            Debug.Log("CharacterIzuna : 资源包已加载");
            loadedObject = loadedBundle.LoadAsset("CharacterIzuna") as GameObject;
            if (loadedObject == null)
            {
                Debug.LogError("CharacterIzuna : 无法加载模型资源!");
                yield break;
            }
            Debug.Log("CharacterIzuna : 模型资源已加载");
            yield return null;
        }

        private void InitializeCharacter(GameObject characterObject)
        {
            characterObject.layer = LayerMask.NameToLayer("Default");
            instantedObject = UnityEngine.Object.Instantiate<GameObject>(characterObject, characterModel.transform);
            instantedObject.transform.localPosition = Vector3.zero;
            instantedObject.transform.position += Vector3.forward * 0.1f;
            characterAnimator = instantedObject.GetComponent<Animator>();
            LoadAllHoldingItemRenderers();
            ReloadHoldingVisual();
            CharacterMainControl.Main.OnAttackEvent += MeleeAnim;
            CharacterMainControl.Main.OnHoldAgentChanged += HoldingItemChanged;
            CharacterMainControl.Main.OnTriggerInputUpdateEvent += TriggerEvent;
            CharacterMainControl.Main.OnActionStartEvent += ActionStart;
        }

        private void ActionStart(CharacterActionBase chara)
        {
            if (characterAnimator == null) return;
            if (chara.ActionPriority() == CharacterActionBase.ActionPriorities.Reload)
            {
                characterAnimator.Play("Reload");
            }
        }

        private void TriggerEvent(bool arg1, bool arg2, bool arg3)
        {
            isTriggerDown = arg1;
            if (arg1)
            {
                CancelInvoke(nameof(CancleTriggerState));
                isTriggerInput = true;
            }
            else
            {
                Invoke(nameof(CancleTriggerState), 0.3f);
            }
        }

        private void CancleTriggerState()
        {
            isTriggerInput = false;
        }

        private void HoldingItemChanged(DuckovItemAgent agent)
        {
            LoadAllHoldingItemRenderers();
            ReloadHoldingVisual();
        }


        private string GetDllDirectory()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }
        private void InitSoundFilePath()
        {
            soundPath.Clear();
            for (int i = 0; i < 99; i++)
            {
                string p = GetDllDirectory() + "/" + i + ".wav";
                if (File.Exists(p))
                {
                    soundPath.Add(p);
                    UnityEngine.Debug.Log("CharacterIzuna ：已加载音频 " + p);
                }
            }
        }

    }
}