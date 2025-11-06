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
        /// <summary>
        /// 嘎嘎叫绑定的新输入
        /// </summary>
        private InputAction newAction = new InputAction();
        /// <summary>
        /// 嘎嘎叫音频文件路径列表
        /// </summary>
        private List<string> soundPath = new List<string>();
        /// <summary>
        /// 嘎嘎叫是否启用
        /// </summary>
        private bool quackEnabled = false;
        /// <summary>
        /// 是否显示武器
        /// </summary>
        private bool isGunActive = true;
        /// <summary>
        /// 是否为射击状态
        /// </summary>
        private bool isTriggerInput = false;
        /// <summary>
        /// 射击键是否按下
        /// </summary>
        private bool isTriggerDown = false;
        /// <summary>
        /// 手持物品的 MeshRenderer
        /// </summary>
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

        /// <summary>
        /// 当玩家更改键位时调用
        /// </summary>
        /// <param name="input"></param>
        private void OnControlsChanged(PlayerInput input)
        {
            //延迟一些执行保证更改的键位已经保存
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
        /// <summary>
        /// 播放一次近战动画
        /// </summary>
        /// <param name="agent"></param>
        private void MeleeAnim(DuckovItemAgent agent)
        {
            if (characterAnimator == null) return;
            characterAnimator.Play("Melee");
        }
        /// <summary>
        /// 更改嘎嘎叫按键的功能
        /// </summary>
        private void InitQuackKey()
        {
            InitSoundFilePath();    //重新检查声音文件
            if (soundPath.Count < 1)
            {
                Debug.Log("CharacterIzuna : 声音文件不存在！");
                return;
            }
            //获取游戏内置的按键输入并禁用
            InputActionAsset actions = GameManager.MainPlayerInput.actions;
            InputAction quackAction = actions.FindAction("Quack");
            quackAction.Disable();
            //创建一个同键位的新输入并绑定事件
            newAction = new InputAction();
            newAction.AddBinding(quackAction.controls[0]);
            newAction.performed += PlaySound;
            newAction.Enable();
        }

        /// <summary>
        /// 禁用自定义嘎嘎叫按键
        /// </summary>
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

        /// <summary>
        /// 发出一些声音
        /// </summary>
        /// <param name="context"></param>
        private void PlaySound(InputAction.CallbackContext context)
        {
            if (CharacterMainControl.Main == null) return;
            //播放列表中随机音频
            int random = UnityEngine.Random.Range(0, soundPath.Count);
            AudioManager.PostCustomSFX(soundPath[random]);

            //在角色位置搞出一点敌人能听到的动静
            AISound sound = new AISound();
            sound.fromCharacter = CharacterMainControl.Main;
            sound.fromObject = base.gameObject;
            sound.pos = CharacterMainControl.Main.characterModel.transform.position;
            sound.fromTeam = 0;
            sound.soundType = SoundTypes.unknowNoise;
            sound.radius = 15f;
            AIMainBrain.MakeSound(sound);
        }
        /// <summary>
        /// 游戏事件文本变化
        /// </summary>
        /// <param name="obj"></param>
        private void OnCommentChanged(string obj)
        {
            if (obj == "Setting character position...")
            {
                //此时代表玩家模型已经加载完毕
                Debug.Log("CharacterIzuna ：准备加载角色模型。");
                ToggleModel(true);
            }
            if (obj == "Starting up...")
            {
                //此时表示正在卸载场景
                ToggleModel(false);
            }
        }
        /// <summary>
        /// 根据父级数量返回制表符，查看场景结构时使用
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
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
        /// <summary>
        /// 替换玩家模型
        /// </summary>
        private void SetModel()
        {
            //初始化参数
            LevelManager instance = LevelManager.Instance;
            characterModel = instance.MainCharacter.characterModel;
            movement = characterModel.characterMainControl.movementControl;
            wasRunning = movement.Running;
            wasMoving = movement.Moving;
            wasDashing = characterModel.characterMainControl.Dashing;
            //隐藏原版模型并加载自定义模型
            HideDuck();
            InitializeCharacter(loadedObject);
        }

        private void RestoreModel()
        {
            //RestoreDuck();
            //ResetAnimation();
        }
        /// <summary>
        /// 隐藏原版玩家角色模型
        /// </summary>
        private void HideDuck()
        {
            Transform transform = characterModel.transform;
            hideGameObject.Clear();
            //根据路径寻找 gameobject 并关闭
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
        /// <summary>
        /// 恢复原版玩家模型
        /// </summary>
        private void RestoreDuck()
        {
            foreach (GameObject gameObject in this.hideGameObject)
            {
                gameObject.SetActive(true);
            }
            this.hideGameObject.Clear();
        }
        /// <summary>
        /// 卸载生成的自定义模型
        /// </summary>
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
        /// <summary>
        /// 重置动画参数
        /// </summary>
        private void ResetAnimation()
        {
            wasMoving = false;
            wasRunning = false;
            wasDashing = false;
        }
        /// <summary>
        /// 根据当前 isGunActive 重载右手中的物品是否显示
        /// </summary>
        private void ReloadHoldingVisual()
        {
            for (int i = 0; i < mrToHide.Count; i++)
            {
                mrToHide[i].enabled = isGunActive;
            }
        }

        /// <summary>
        /// 查找所有手持物品的 MeshRenderer
        /// </summary>
        private void LoadAllHoldingItemRenderers()
        {
            Transform targetTransform = CharacterMainControl.Main.RightHandSocket;
            mrToHide.Clear();
            LoadRenderers(targetTransform);
        }
        /// <summary>
        /// 递归查找 target 下所有 MeshRenderer
        /// </summary>
        /// <param name="target"></param>
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

            //翻滚一次
            if (dashing && dashing != wasDashing) characterAnimator.Play("Dash");

            //地堡奔跑速度恒定为7.5582
            //角色当前移动速度为 CharacterMainControl.Main.Velocity.magnitude
            //角色当前最大奔跑速度为 CharacterMainControl.Main.CharacterRunSpeed
            //speed参数应夹在0.1到1.3之间
            //或者根据实际情况调整

            characterAnimator.SetBool("Running", true);
            float speedProgress = CharacterMainControl.Main.Velocity.magnitude / 7f;
            float speedParam = Mathf.Lerp(0.01f, 1.3f, speedProgress);

            if (moving || running) characterAnimator.SetFloat("MovingSpeed", speedParam);
            else characterAnimator.SetFloat("MovingSpeed", 0);


            wasMoving = moving;
            wasRunning = running;
            wasDashing = dashing;
            //使用右键瞄准或者正在腰射状态
            bool aiming = CharacterMainControl.Main.IsInAdsInput || isTriggerInput;
            characterAnimator.SetBool("Aiming", aiming);

            characterAnimator.SetFloat("Attack", isTriggerDown ? 0.99f : 0f);
        }
        /// <summary>
        /// 读取AB包协程
        /// </summary>
        /// <returns></returns>
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
        /// <summary>
        /// 初始化自定义角色模型
        /// </summary>
        /// <param name="characterObject"></param>
        private void InitializeCharacter(GameObject characterObject)
        {
            characterObject.layer = LayerMask.NameToLayer("Default");
            instantedObject = UnityEngine.Object.Instantiate<GameObject>(characterObject, characterModel.transform);
            instantedObject.transform.localPosition = Vector3.zero;
            instantedObject.transform.position += Vector3.forward * 0.1f;
            characterAnimator = instantedObject.GetComponent<Animator>();
            LoadAllHoldingItemRenderers();
            ReloadHoldingVisual();
            //注册相关事件
            CharacterMainControl.Main.OnAttackEvent += MeleeAnim;
            CharacterMainControl.Main.OnHoldAgentChanged += HoldingItemChanged;
            CharacterMainControl.Main.OnTriggerInputUpdateEvent += TriggerEvent;
            CharacterMainControl.Main.OnActionStartEvent += ActionStart;
        }
        /// <summary>
        /// 角色行为开始时
        /// </summary>
        /// <param name="chara"></param>
        private void ActionStart(CharacterActionBase chara)
        {
            if (characterAnimator == null) return;
            //判断是否为换弹行为
            if (chara.ActionPriority() == CharacterActionBase.ActionPriorities.Reload)
            {
                characterAnimator.Play("Reload");
            }
        }
        /// <summary>
        /// 当玩家设计状态变化时
        /// </summary>
        /// <param name="arg1">是否正在射击</param>
        /// <param name="arg2">当前帧是否为触发</param>
        /// <param name="arg3">当前帧是否为释放</param>
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
                //连续点射状态下延迟一些时间恢复为释放状态
                Invoke(nameof(CancleTriggerState), 0.3f);
            }
        }
        /// <summary>
        /// 取消射击状态
        /// </summary>
        private void CancleTriggerState()
        {
            isTriggerInput = false;
        }
        /// <summary>
        /// 玩家手持物品变化时
        /// </summary>
        /// <param name="agent"></param>
        private void HoldingItemChanged(DuckovItemAgent agent)
        {
            LoadAllHoldingItemRenderers();
            ReloadHoldingVisual();
        }

        /// <summary>
        /// dll 文件的路径
        /// </summary>
        /// <returns></returns>
        private string GetDllDirectory()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }
        /// <summary>
        /// 加载所有可用的音频文件，只要文件名为 (0-99的数字).wav 就可以读取
        /// </summary>
        private void InitSoundFilePath()
        {
            soundPath.Clear();
            for (int i = 0; i < 99; i++)
            {
                string p = GetDllDirectory() + "/" + i + ".wav";
                if (File.Exists(p))
                {
                    soundPath.Add(p);
                    Debug.Log("CharacterIzuna ：已加载音频 " + p);
                }
            }
        }

    }
}