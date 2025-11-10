using Duckov;
using ItemStatsSystem.Items;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.GraphicsBuffer;

namespace CharacterIzuna
{

    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        private CharacterModel characterModel;

        private Movement movement;

        //隐藏模型逻辑直接改版，这些不要了
        //private string[] pathsToHide = new string[]
        //{
        //    "CustomFaceInstance/DuckBody",
        //    "CustomFaceInstance/Armature/Root/Pelvis/Thigh.L",
        //    "CustomFaceInstance/Armature/Root/Pelvis/Thigh.R",
        //    "CustomFaceInstance/Armature/Root/Pelvis/Spine.001/Spine.002/Spine.003/Spine.004/Head",
        //    "CustomFaceInstance/Armature/Root/Pelvis/Spine.001/Spine.002/Spine.003/Spine.004/UpperArm.L/Wings.L",
        //    "CustomFaceInstance/Armature/Root/Pelvis/Spine.001/Spine.002/Spine.003/Spine.004/UpperArm.R/Wings.R",
        //    "CustomFaceInstance/Armature/Root/Pelvis/Spine.001/Spine.002/Spine.003/Spine.004/ArmorSocket",
        //    "CustomFaceInstance/Armature/Root/Pelvis/Spine.001/Spine.002/Spine.003/Spine.004/BackpackSocket",
        //    "CustomFaceInstance/Armature/Root/Pelvis/TailSocket"
        //};

        /// <summary>
        ///装备槽位和对应的 transform 
        /// </summary>
        private Dictionary<string, Transform> slotTransforms = new Dictionary<string, Transform>();

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

        private string targetShaderName = "SodaCraft/SodaCharacter";



        //private List<MeshRenderer> characterMr = new List<MeshRenderer>();
        //private List<SkinnedMeshRenderer> characterSmr = new List<SkinnedMeshRenderer>();


        private void Update()
        {
            if (instantedObject != null) UpdateAnimation();
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.H))
            {
                isGunActive = !isGunActive;
                LoadAllHoldingItemRenderers();
                ReloadHoldingVisual();
            }

            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.M)) SetCharacterShader();
            //if (Input.GetKeyDown(KeyCode.L))
            //{
            //    Debug.Log("CharacterMainControl.Main.modelRoot : " + CharacterMainControl.Main.modelRoot.name);

            //    characterMr.Clear();
            //    characterSmr.Clear();
            //    LogName(characterModel.transform.parent);
            //    Debug.Log($"找到 {characterMr.Count} 个角色模型MeshRenderer");
            //    for (int i = 0; i < characterMr.Count; i++) Debug.Log($"{i + 1} : {characterMr[i].gameObject.name}");
            //    Debug.Log($"找到 {characterSmr.Count} 个角色模型SkinnedMeshRenderer");
            //    for (int i = 0; i < characterSmr.Count; i++) Debug.Log($"{i + 1} : {characterSmr[i].gameObject.name}");
            //}
            //测试切换手持物品为空测试
            //if (Input.GetKeyDown(KeyCode.Keypad0))
            //{
            //    if (CharacterMainControl.Main.CurrentHoldItemAgent == null) Debug.Log("CurrentHoldItemAgent is null");
            //    else Debug.Log("CurrentHoldItemAgent is " + CharacterMainControl.Main.CurrentHoldItemAgent.name);

            //    CharacterMainControl.Main.ChangeHoldItem(null);
            //}
        }


        /// <summary>
        /// 测试输出结构
        /// </summary>
        /// <param name="target"></param>
        //private void LogName(Transform target)
        //{
        //    string result = string.Format("{0}{1}", GetSpace(target), target.name);
        //    Debug.Log(result);

        //    MeshRenderer mr = target.GetComponent<MeshRenderer>();
        //    SkinnedMeshRenderer smr = target.GetComponent<SkinnedMeshRenderer>();
        //    if (mr != null) characterMr.Add(mr);
        //    if (smr != null) characterSmr.Add(smr);
        //    if (target.childCount > 0)
        //    {
        //        for (int i = 0; i < target.childCount; i++)
        //        {
        //            LogName(target.GetChild(i));
        //        }
        //    }
        //}

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
            CharacterMainControl.OnMainCharacterSlotContentChangedEvent -= SlotContentChanged;
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
            CollectSlotTransforms();
            //隐藏原版模型并加载自定义模型
            HideDuck();
            InitializeCharacter(loadedObject);
        }

        /// <summary>
        /// 生成所有要隐藏的槽位信息
        /// </summary>
        private void CollectSlotTransforms()
        {
            slotTransforms.Clear();
            //需要隐藏的槽位
            //Helmat
            slotTransforms.Add("Helmat", characterModel.HelmatSocket);
            //Armor
            slotTransforms.Add("Armor", characterModel.ArmorSocket);
            //FaceMask
            slotTransforms.Add("FaceMask", characterModel.FaceMaskSocket);
            //Headset
            slotTransforms.Add("Headset", characterModel.HelmatSocket);
            //Backpack
            slotTransforms.Add("Backpack", characterModel.BackpackSocket);
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
            Transform playerModel = characterModel.transform;
            SkinnedMeshRenderer duckBody = playerModel.Find("CustomFaceInstance/DuckBody").GetComponent<SkinnedMeshRenderer>();
            if (duckBody != null) duckBody.enabled = false;
            SetAllMeshRendererInChild(playerModel, false);
        }

        /// <summary>
        /// 禁用一个 Transform 下所有 MeshRenderer
        /// </summary>
        /// <param name="target"></param>
        private void SetAllMeshRendererInChild(Transform target, bool active)
        {
            if (target.name.Contains("CharacterIzuna")) return;
            MeshRenderer mr = target.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = active;
            if (target.childCount > 0)
            {
                for (int i = 0; i < target.childCount; i++)
                {
                    SetAllMeshRendererInChild(target.GetChild(i), active);
                }
            }
        }

        /// <summary>
        /// 恢复原版玩家模型
        /// </summary>
        private void RestoreDuck()
        {
            Transform playerModel = characterModel.transform;
            SkinnedMeshRenderer duckBody = playerModel.Find("CustomFaceInstance/DuckBody").GetComponent<SkinnedMeshRenderer>();
            if (duckBody != null) duckBody.enabled = true;
            SetAllMeshRendererInChild(playerModel, true);
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
            CharacterMainControl.OnMainCharacterSlotContentChangedEvent += SlotContentChanged;
            if (Application.platform == RuntimePlatform.OSXPlayer)
            {
                Debug.Log("当前运行环境为 Mac ，自动替换 Shader");
                SetCharacterShader();
            }
        }
        /// <summary>
        /// 玩家装备槽位变动事件
        /// </summary>
        /// <param name="control"></param>
        /// <param name="slot"></param>
        private void SlotContentChanged(CharacterMainControl control, Slot slot)
        {
            //先检查这个槽位是否是需要隐藏模型的槽位
            if (!slotTransforms.ContainsKey(slot.Key)) return;
            StartCoroutine(OnSlotContentChangedIEnumerator(slot.Key));
        }
        /// <summary>
        /// 装备变更并非即时生效，等待下一帧执行
        /// </summary>
        /// <param name="slotName"></param>
        /// <returns></returns>
        IEnumerator OnSlotContentChangedIEnumerator(string slotName)
        {
            yield return new WaitForEndOfFrame();
            SetAllMeshRendererInChild(slotTransforms[slotName], false);
        }

        /// <summary>
        /// 角色行为开始时
        /// </summary>
        /// <param name="chara"></param>
        private void ActionStart(CharacterActionBase chara)
        {
            //Debug.Log(chara.ActionPriority());
            if (characterAnimator == null) return;
            //判断是否为换弹行为
            if (chara.ActionPriority() == CharacterActionBase.ActionPriorities.Reload)
            {
                characterAnimator.Play("Reload");
            }
        }
        /// <summary>
        /// 当玩家射击状态变化时
        /// </summary>
        /// <param name="arg1">是否正在射击</param>
        /// <param name="arg2">当前帧是否为触发</param>
        /// <param name="arg3">当前帧是否为释放</param>
        private void TriggerEvent(bool arg1, bool arg2, bool arg3)
        {
            //如果当前持有的是近战武器，射击状态不会改变
            if (CharacterMainControl.Main.CurrentHoldItemAgent != null)
            {
                if (CharacterMainControl.Main.CurrentHoldItemAgent.handAnimationType == HandheldAnimationType.meleeWeapon) return;
            }
            else return;
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
            if (agent == null) return;
            LoadAllHoldingItemRenderers();
            ReloadHoldingVisual();
        }

        private void SetCharacterShader()
        {
            Shader shader = Shader.Find(targetShaderName);
            if (shader == null) 
            {
                Debug.LogError("Shader not found: " + targetShaderName);
                return;
            }
            else
            {
                for (int i = 0; i < characterModel.transform.childCount; i++)
                {
                    if (characterModel.transform.GetChild(i).name.Contains("Izuna"))
                    {
                        ReplaceAllShaders(characterModel.transform.GetChild(i).Find("Izuna_Original_Mesh/Izuna_Original_Body"), shader);
                        ReplaceAllShaders(characterModel.transform.GetChild(i).Find("Izuna_Original_Mesh/Izuna_Original_Shuriken_Outline"), shader);
                        ReplaceAllShaders(characterModel.transform.GetChild(i).Find("Izuna_Original_Mesh/Izuna_Original_Weapon"), shader);
                    }
                }
            }
        }
        private void ReplaceAllShaders(Transform target, Shader shader)
        {
            Renderer r = target.GetComponent<Renderer>();
            if (r == null) return;

            foreach (Material material in r.materials)
            {
                if (material != null)
                {
                    Texture mainTex = material.GetTexture("_BaseMap");
                    material.shader = shader;
                    material.SetTexture("_MainTex", mainTex);
                    material.SetFloat("_AlphaCutoff", 0.75f);
                    material.SetFloat("_Metallic", 0);
                    material.SetFloat("_Smoothness", 0);
                    if (material.name.Contains("EyeMouth"))
                        material.SetTexture("_EmissionMap", mainTex);
                    else
                        material.SetColor("_EmissionColor", Color.black);
                }
            }
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

//角色结构
//----0_CharacterModel_Custom_Template(Clone)
//------CustomFaceInstance
//--------Armature
//----------Root
//------------Pelvis
//--------------Spine.001
//----------------Spine.002
//------------------Spine.003
//--------------------Spine.004
//----------------------Head
//------------------------Duck_Beak
//------------------------Duck_Eye.L
//------------------------Duck_Eye.R
//------------------------HeadTip
//--------------------------HeadTip_end
//------------------------HelmatSocket
//--------------------------HeadCollider(Clone)
//------------------------FaceMaskSocket
//------------------------HairSocket
//--------------------------HeadPart 06 Kun(Clone)
//----------------------------Mouth
//------------------------------Hair_Kunkun
//------------------------MouthSocket
//--------------------------Mouth_2(Clone)
//----------------------------Center
//------------------------------Duck_Beak
//------------------------TestEyePart 6_a(Clone)
//--------------------------LeftEye
//----------------------------Face_Eye_Type_02
//--------------------------RightEye
//----------------------------Face_Eye_Type_03
//------------------------TestEyebrowPart 6(Clone)
//--------------------------LeftEye
//----------------------------Face_Eyebrow_Type_02
//--------------------------RightEye
//----------------------------Face_Eyebrow_Type_03
//----------------------UpperArm.L
//------------------------Elbow.L
//--------------------------ForeArm.L
//----------------------------Hand.L
//------------------------------Hand.Soket.L
//--------------------------------Hand.Soket.L_end
//--------------------------------LeftHandSocket
//------------------------------HandObj.L
//------------------------Wings.L
//--------------------------0_Wing_default(Clone)
//----------------------------Root
//------------------------------Wings.L_1
//----------------------UpperArm.R
//------------------------Elbow.R
//--------------------------ForeArm.R
//----------------------------Hand.R
//------------------------------Hand.Soket.R
//--------------------------------Hand.Soket.R_end
//--------------------------------RightHandSocket
//--------------------------------TestBowSocket
//--------------------------------MeleeWeaponSocket
//----------------------------------MeleeWeaponSocketFixed
//------------------------------HandObj.R
//------------------------Wings.R
//--------------------------0_Wing_default(Clone)
//----------------------------Root
//------------------------------Wings.L_1
//----------------------ArmorSocket
//----------------------BackpackSocket
//--------------Tail
//----------------Tail.001
//------------------Tail.001_end
//--------------Thigh.L
//----------------Foot.L
//------------------Duck_Foot_L
//------------------Foot.L_end
//------------------Sphere (1)
//------------------FootLSocket
//--------------------0_FootDefault_L(Clone)
//----------------------ScaleRoot
//------------------------DuckFoot
//--------------Thigh.R
//----------------Foot.R
//------------------Duck_Foot_R
//------------------Foot.R_end
//------------------Sphere
//------------------FootRSocket
//--------------------0_FootDefault_L(Clone)
//----------------------ScaleRoot
//------------------------DuckFoot
//--------------TailSocket
//----------------Tail_1(Clone)
//------------------Center
//--------------------Crest_II.003
//--------DuckBody
//--------Arms
//----------Line
//----------Line (1)
//----------Line (2)
//----------Line (3)
//------RunParticle
//--------Particle System
//------PopTextSocket
//------GrassCollider
//------CharacterIzuna(Clone)
//--------Izuna_Original_Mesh
//----------bone_Boom_01
//------------bone_Boom_02
//--------------bone_Boom_03
//----------------bone_Boom_04
//----------bone_root
//------------Bip001
//--------------Bip001 Pelvis
//----------------Bip001 L Thigh
//------------------Bip001 L Calf
//--------------------Bip001 L Foot
//----------------------Bip001 L Toe0
//----------------Bip001 R Thigh
//------------------Bip001 R Calf
//--------------------Bip001 R Foot
//----------------------Bip001 R Toe0
//----------------Bip001 Spine
//------------------Bip001 Spine1
//--------------------Bip001 L Clavicle
//----------------------Bip001 L UpperArm
//------------------------Bip001 L Forearm
//--------------------------Bip001 L Hand
//----------------------------Bip001 L Finger0
//------------------------------Bip001 L Finger01
//----------------------------Bip001 L Finger1
//------------------------------Bip001 L Finger11
//----------------------------Bip001 L Finger2
//------------------------------Bip001 L Finger21
//----------------------------Bip001 L Finger3
//------------------------------Bip001 L Finger31
//----------------------------Bip001 L Finger4
//------------------------------Bip001 L Finger41
//--------------------------Bone L ForeArm Twist
//----------------------Bip001_B_L Deltoid
//--------------------Bip001 Neck
//----------------------Bip001 Head
//------------------------Bip001 bone_eye_D_L
//--------------------------Bip001 bone_eye_D_L_01
//--------------------------Bip001 bone_eye_D_L_02
//------------------------Bip001 bone_eye_D_R
//--------------------------Bip001 bone_eye_D_R_01
//--------------------------Bip001 bone_eye_D_R_02
//------------------------Bip001 eye_L
//--------------------------Bip001 eye_L_1
//--------------------------Bip001 eye_L_2
//------------------------Bip001 eye_R
//--------------------------Bip001 eye_R_1
//--------------------------Bip001 eye_R_2
//------------------------Bip001 Xtra_eyeblowL1
//------------------------Bip001 Xtra_eyeblowL2
//------------------------Bip001 Xtra_eyeblowR1
//------------------------Bip001 Xtra_eyeblowR2
//------------------------Bip001 Xtra_eyeL
//------------------------Bip001 Xtra_eyeR
//------------------------bone_ear_L_01
//--------------------------bone_ear_L_02
//------------------------bone_ear_R_01
//--------------------------bone_ear_R_02
//------------------------bone_hair_f_01
//--------------------------bone_hair_f_02
//------------------------bone_hair_f_l_01
//--------------------------bone_hair_f_l_02
//------------------------bone_hair_f_r_01
//--------------------------bone_hair_f_r_02
//------------------------bone_hair_m_l_01
//--------------------------bone_hair_m_l_02
//----------------------------bone_hair_m_l_03
//----------------------------bone_hairdecor_04
//------------------------bone_hair_m_r_01
//--------------------------bone_hair_m_r_02
//----------------------------bone_hair_m_r_03
//------------------------bone_hair_R_01
//--------------------------bone_hair_R_02
//----------------------------bone_hair_R_03
//--------------------------bone_hairdecor_01
//----------------------------bone_hairdecor_02
//------------------------------bone_hairdecor_03
//------------------------Izuna_Original_Halo
//--------------------------Izuna_Original_Halo_0
//--------------------Bip001 R Clavicle
//----------------------Bip001 R UpperArm
//------------------------Bip001 R Forearm
//--------------------------Bip001 R Hand
//----------------------------Bip001 R Finger0
//------------------------------Bip001 R Finger01
//----------------------------Bip001 R Finger1
//------------------------------Bip001 R Finger11
//----------------------------Bip001 R Finger2
//------------------------------Bip001 R Finger21
//----------------------------Bip001 R Finger3
//------------------------------Bip001 R Finger31
//----------------------------Bip001 R Finger4
//------------------------------Bip001 R Finger41
//--------------------------Bone R ForeArm Twist
//--------------------------bone_Sodetake_R_01
//----------------------------bone_Sodetake_R_02
//----------------------Bip001_B_R Deltoid
//--------------------bone_Muffler_L_01
//----------------------bone_Muffler_L_02
//------------------------bone_Muffler_L_03
//--------------------------bone_Muffler_L_04
//----------------------------bone_Muffler_L_05
//------------------------------bone_Muffler_L_06
//--------------------bone_Muffler_R_01
//----------------------bone_Muffler_R_02
//------------------------bone_Muffler_R_03
//--------------------------bone_Muffler_R_04
//----------------------------bone_Muffler_R_05
//------------------------------bone_Muffler_R_06
//--------------------bone_Tie_01
//----------------------bone_Tie_02
//----------------bone_Ribbon_DL_01
//------------------bone_Ribbon_DL_02
//----------------bone_Ribbon_DR_01
//------------------bone_Ribbon_DR_02
//----------------bone_Ribbon_UL_01
//------------------bone_Ribbon_UL_02
//----------------bone_Ribbon_UR_01
//------------------bone_Ribbon_UR_02
//----------------bone_skirtB00
//------------------bone_skirtB01
//--------------------bone_skirtB02
//----------------------bone_skirtB03
//----------------bone_skirtB_L_00
//------------------bone_skirtB_L_01
//--------------------bone_skirtB_L_02
//----------------------bone_skirtB_L_03
//----------------bone_skirtB_R_00
//------------------bone_skirtB_R_01
//--------------------bone_skirtB_R_02
//----------------------bone_skirtB_R_03
//----------------bone_skirtF00
//------------------bone_skirtF01
//--------------------bone_skirtF02
//----------------bone_skirtF_L_00
//------------------bone_skirtF_L_01
//--------------------bone_skirtF_L_02
//----------------------bone_skirtF_L_03
//----------------bone_skirtF_R_00
//------------------bone_skirtF_R_01
//--------------------bone_skirtF_R_02
//----------------------bone_skirtF_R_03
//----------------bone_skirtL00
//------------------bone_skirtL01
//--------------------bone_skirtL02
//----------------------bone_skirtL03
//----------------bone_skirtR00
//------------------bone_skirtR01
//--------------------bone_skirtR02
//----------------------bone_skirtR03
//----------------bone_Sodehaba_L_01
//------------------bone_Sodehaba_L_02
//----------------bone_Tail_01
//------------------bone_Tail_02
//--------------------bone_Tail_03
//----------------------bone_Tail_04
//------------------------bone_Tail_05
//--------------Bip001_Weapon
//----------------bone_buttstock
//----------------bone_Handle
//----------------bone_magazine_01
//----------------bone_Weapondecor_01
//----------------bone_Weapondecor_02
//----------------bone_Weapondecor_03
//------------------bone_Weapondecor_04
//----------------bone_Weapondecor_05
//------------------bone_Weapondecor_06
//----------------fire_01
//----------------fire_02
//------------fire_03
//----------Izuna_Original_Body
//----------Izuna_Original_Shuriken_Outline
//----------Izuna_Original_Weapon
//找到 22 个角色模型MeshRenderer
//1 : Duck_Beak
//2 : Duck_Eye.L
//3 : Duck_Eye.R
//4 : Hair_Kunkun
//5 : Duck_Beak
//6 : Face_Eye_Type_02
//7 : Face_Eye_Type_03
//8 : Face_Eyebrow_Type_02
//9 : Face_Eyebrow_Type_03
//10 : HandObj.L
//11 : Wings.L_1
//12 : HandObj.R
//13 : Wings.L_1
//14 : Duck_Foot_L
//15 : Sphere(1)
//16 : DuckFoot
//17 : Duck_Foot_R
//18 : Sphere
//19 : DuckFoot
//20 : Crest_II.003
//21 : CharacterIzuna(Clone)
//22 : Izuna_Original_Halo_0
//找到 4 个角色模型SkinnedMeshRenderer
//1 : DuckBody
//2 : Izuna_Original_Body
//3 : Izuna_Original_Shuriken_Outline
//4 : Izuna_Original_Weapon