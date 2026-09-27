#!/usr/bin/env python3
# ---------------------------------------------------------------------------
# make_unity_project.py —— 生成/补全 Unity 工程结构:
#   1. 为 Assets 下所有文件与目录生成确定性 .meta(GUID = md5(路径));
#   2. 生成 Assets/Scenes/Main.unity(唯一场景:相机/事件系统/Canvas/运行器);
#   3. 生成 ProjectSettings(2019.4 兼容)与 ProjectVersion;
#   4. 生成 .gitignore。
# 可重复执行(幂等):已存在的 .meta 不覆盖。
# ---------------------------------------------------------------------------
import hashlib
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "Assets")
SETTINGS = os.path.join(ROOT, "ProjectSettings")
SCENE_PATH = os.path.join(ASSETS, "Scenes", "Main.unity")

HEADER = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"


def guid_for(relpath: str) -> str:
    """确定性 GUID:同一文件永远同一 GUID。"""
    return hashlib.md5(("planewar:" + relpath.replace("\\", "/")).encode()).hexdigest()


# ---------------------------------------------------------------------------
# .meta 生成
# ---------------------------------------------------------------------------

MONO_META = """fileFormatVersion: 2
guid: {guid}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

SCENE_META = """fileFormatVersion: 2
guid: {guid}
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def ensure_meta(path: str, is_folder: bool):
    meta = path + ".meta"
    if os.path.exists(meta):
        return
    rel = os.path.relpath(path, ROOT).replace("\\", "/")
    g = guid_for(rel)
    if is_folder:
        content = FOLDER_META.format(guid=g)
    elif path.endswith(".unity"):
        content = SCENE_META.format(guid=g)
    else:
        content = MONO_META.format(guid=g)
    with open(meta, "w", newline="\n") as f:
        f.write(content)


def gen_metas():
    # 目录(自顶向下,保证父目录先有 meta)
    for dirpath, dirnames, filenames in os.walk(ASSETS):
        if dirpath != ASSETS:
            ensure_meta(dirpath, True)
        for fn in filenames:
            if fn.endswith(".meta"):
                continue
            ensure_meta(os.path.join(dirpath, fn), False)
    ensure_meta(ASSETS, True)


# ---------------------------------------------------------------------------
# 场景生成
# ---------------------------------------------------------------------------

def script_ref(cs_relpath: str) -> str:
    return "{fileID: 11500000, guid: %s, type: 3}" % guid_for(cs_relpath)


def build_scene() -> str:
    cam_rig = script_ref("Assets/Scripts/Gameplay/CameraRig.cs")
    docs = []
    # 注意:%TAG 指令与第一个 "---" 之间不得有空行,否则句柄失效
    docs.append(HEADER.rstrip("\n"))

    # ---- 内置场景设置 ----
    docs.append("""--- !u!29 &1
OcclusionCullingSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_OcclusionBakeSettings:
    smallestOccluder: 5
    smallestHole: 0.25
    backfaceThreshold: 100
  m_SceneGUID: 00000000000000000000000000000000
  m_OcclusionCullingData: {fileID: 0}""")

    docs.append("""--- !u!104 &2
RenderSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 9
  m_Fog: 0
  m_FogColor: {r: 0.5, g: 0.5, b: 0.5, a: 1}
  m_FogMode: 3
  m_FogDensity: 0.01
  m_LinearFogStart: 0
  m_LinearFogEnd: 300
  m_AmbientSkyColor: {r: 0.212, g: 0.227, b: 0.259, a: 1}
  m_AmbientEquatorColor: {r: 0.114, g: 0.125, b: 0.133, a: 1}
  m_AmbientGroundColor: {r: 0.047, g: 0.043, b: 0.035, a: 1}
  m_AmbientIntensity: 1
  m_AmbientMode: 3
  m_SubtractiveShadowColor: {r: 0.42, g: 0.478, b: 0.627, a: 1}
  m_SkyboxMaterial: {fileID: 0}
  m_HaloStrength: 0.5
  m_FlareStrength: 1
  m_FlareFadeSpeed: 3
  m_HaloTexture: {fileID: 0}
  m_SpotCookie: {fileID: 10001, guid: 0000000000000000e000000000000000, type: 0}
  m_DefaultReflectionMode: 0
  m_DefaultReflectionResolution: 128
  m_ReflectionBounces: 1
  m_ReflectionIntensity: 1
  m_CustomReflection: {fileID: 0}
  m_Sun: {fileID: 0}
  m_IndirectSpecularColor: {r: 0, g: 0, b: 0, a: 1}
  m_UseRadianceAmbientProbe: 0""")

    docs.append("""--- !u!157 &3
LightmapSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 11
  m_GIWorkflowMode: 1
  m_GISettings:
    serializedVersion: 2
    m_BounceScale: 1
    m_IndirectOutputScale: 1
    m_AlbedoBoost: 1
    m_EnvironmentLightingMode: 0
    m_EnableBakedLightmaps: 0
    m_EnableRealtimeLightmaps: 0
  m_LightmapEditorSettings:
    serializedVersion: 12
    m_Resolution: 2
    m_BakeResolution: 40
    m_AtlasSize: 1024
    m_AO: 0
    m_AOMaxDistance: 1
    m_CompAOExponent: 1
    m_CompAOExponentDirect: 0
    m_ExtractAmbientOcclusion: 0
    m_Padding: 2
    m_LightmapParameters: {fileID: 0}
    m_LightmapsBakeMode: 1
    m_TextureCompression: 1
    m_FinalGather: 0
    m_FinalGatherFiltering: 1
    m_FinalGatherRayCount: 256
    m_ReflectionCompression: 2
    m_MixedBakeMode: 2
    m_BakeBackend: 1
    m_PVRSampling: 1
    m_PVRDirectSampleCount: 32
    m_PVRSampleCount: 512
    m_PVRBounces: 2
    m_PVREnvironmentSampleCount: 256
    m_PVREnvironmentReferencePointCount: 2048
    m_PVRFilteringMode: 1
    m_PVRDenoiserTypeDirect: 1
    m_PVRDenoiserTypeIndirect: 1
    m_PVRDenoiserTypeAO: 1
    m_PVRFilterTypeDirect: 0
    m_PVRFilterTypeIndirect: 0
    m_PVRFilterTypeAO: 0
    m_PVREnvironmentMIS: 1
    m_PVRCulling: 1
    m_PVRFilteringGaussRadiusDirect: 1
    m_PVRFilteringGaussRadiusIndirect: 5
    m_PVRFilteringGaussRadiusAO: 2
    m_PVRFilteringAtrousPositionSigmaDirect: 0.5
    m_PVRFilteringAtrousPositionSigmaIndirect: 2
    m_PVRFilteringAtrousPositionSigmaAO: 1
    m_ExportTrainingData: 0
    m_TrainingDataDestination: TrainingData
    m_LightProbeSampleCountMultiplier: 4
  m_LightingDataAsset: {fileID: 0}
  m_UseShadowmask: 1""")

    docs.append("""--- !u!196 &4
NavMeshSettings:
  serializedVersion: 2
  m_ObjectHideFlags: 0
  m_BuildSettings:
    serializedVersion: 2
    agentTypeID: 0
    agentRadius: 0.5
    agentHeight: 2
    agentSlope: 45
    agentClimb: 0.4
    ledgeDropHeight: 0
    maxJumpAcrossDistance: 0
    minRegionArea: 2
    manualCellSize: 0
    cellSize: 0.16666667
    manualTileSize: 0
    tileSize: 256
    accuratePlacement: 0
    debug:
      m_Flags: 0
  m_NavMeshData: {fileID: 0}""")

    # ---- Main Camera ----
    docs.append("""--- !u!1 &10
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 11}
  - component: {fileID: 12}
  - component: {fileID: 13}
  - component: {fileID: 14}
  m_Layer: 0
  m_Name: Main Camera
  m_TagString: MainCamera
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1""")

    docs.append("""--- !u!4 &11
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 10}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 4.5, y: 8, z: -10}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_Children: []
  m_Father: {fileID: 0}
  m_RootOrder: 0
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}""")

    docs.append("""--- !u!20 &12
Camera:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 10}
  m_Enabled: 1
  serializedVersion: 2
  m_ClearFlags: 2
  m_BackGroundColor: {r: 0.012, g: 0.02, b: 0.05, a: 1}
  m_projectionMatrixMode: 1
  m_GateFitMode: 2
  m_FOVAxisMode: 0
  m_SensorSize: {x: 36, y: 24}
  m_LensShift: {x: 0, y: 0}
  m_FocalLength: 50
  m_NormalizedViewPortRect:
    serializedVersion: 2
    x: 0
    y: 0
    width: 1
    height: 1
  near clip plane: 0.3
  far clip plane: 1000
  field of view: 60
  orthographic: 1
  orthographic size: 8
  m_Depth: -1
  m_CullingMask:
    serializedVersion: 2
    m_Bits: 4294967295
  m_RenderingPath: -1
  m_TargetTexture: {fileID: 0}
  m_TargetDisplay: 0
  m_TargetEye: 3
  m_HDR: 0
  m_AllowMSAA: 0
  m_AllowDynamicResolution: 0
  m_ForceIntoRT: 0
  m_OcclusionCulling: 0
  m_StereoConvergence: 10
  m_StereoSeparation: 0.022""")

    docs.append("""--- !u!81 &13
AudioListener:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 10}
  m_Enabled: 1""")

    docs.append("""--- !u!114 &14
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 10}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: %s
  m_Name: 
  m_EditorClassIdentifier: """ % cam_rig)

    # ---- EventSystem ----
    docs.append("""--- !u!1 &20
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 21}
  - component: {fileID: 22}
  - component: {fileID: 23}
  m_Layer: 0
  m_Name: EventSystem
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1""")

    docs.append("""--- !u!4 &21
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 20}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_Children: []
  m_Father: {fileID: 0}
  m_RootOrder: 1
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}""")

    docs.append("""--- !u!114 &22
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 20}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 76c392e42b5098c458856cdf6ecaaaa1, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  m_FirstSelected: {fileID: 0}
  m_sendNavigationEvents: 1
  m_DragThreshold: 10""")

    docs.append("""--- !u!114 &23
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 20}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 4f231c4fb786f3946a6b90b886c48677, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  m_HorizontalAxis: Horizontal
  m_VerticalAxis: Vertical
  m_SubmitButton: Submit
  m_CancelButton: Cancel
  m_InputActionsPerSecond: 10
  m_RepeatDelay: 0.5
  m_ForceModuleActive: 0""")

    # ---- GameCanvas ----
    docs.append("""--- !u!1 &30
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 31}
  - component: {fileID: 32}
  - component: {fileID: 33}
  - component: {fileID: 34}
  - component: {fileID: 35}
  m_Layer: 5
  m_Name: GameCanvas
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1""")

    docs.append("""--- !u!224 &31
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 30}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 0, y: 0, z: 0}
  m_Children:
  - {fileID: 41}
  - {fileID: 51}
  - {fileID: 56}
  m_Father: {fileID: 0}
  m_RootOrder: 2
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0, y: 0}
  m_AnchorMax: {x: 0, y: 0}
  m_AnchoredPosition: {x: 0, y: 0}
  m_SizeDelta: {x: 0, y: 0}
  m_Pivot: {x: 0, y: 0}""")

    docs.append("""--- !u!223 &32
Canvas:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 30}
  m_Enabled: 1
  serializedVersion: 3
  m_RenderMode: 0
  m_Camera: {fileID: 0}
  m_PlaneDistance: 100
  m_PixelPerfect: 0
  m_ReceivesEvents: 1
  m_OverrideSorting: 0
  m_OverridePixelPerfect: 0
  m_SortingBucketNormalizedSize: 0
  m_AdditionalShaderChannelsFlag: 0
  m_SortingLayerID: 0
  m_SortingOrder: 0
  m_TargetDisplay: 0""")

    docs.append("""--- !u!114 &33
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 30}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 0cd44c1031e13a943bb63640046fad76, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  m_UiScaleMode: 1
  m_ReferencePixelsPerUnit: 100
  m_ScaleFactor: 1
  m_ReferenceResolution: {x: 900, y: 1600}
  m_ScreenMatchMode: 0
  m_MatchWidthOrHeight: 0.5
  m_PhysicalUnit: 3
  m_FallbackScreenDPI: 96
  m_DefaultSpriteDPI: 96
  m_DynamicPixelsPerUnit: 1""")

    docs.append("""--- !u!114 &34
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 30}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: dc42784cf147c0c48a680349fa168899, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  m_IgnoreReversedGraphics: 1
  m_BlockingObjects: 0
  m_BlockingMask:
    serializedVersion: 2
    m_Bits: 4294967295""")

    docs.append("""--- !u!114 &35
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 30}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: %s
  m_Name: 
  m_EditorClassIdentifier: """ % script_ref("Assets/Scripts/UI/UIManager.cs"))

    # ---- TouchLayer(拖拽输入捕获层) ----
    docs.append("""--- !u!1 &40
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 41}
  - component: {fileID: 42}
  - component: {fileID: 43}
  m_Layer: 5
  m_Name: TouchLayer
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1""")

    docs.append("""--- !u!224 &41
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 40}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_Children: []
  m_Father: {fileID: 31}
  m_RootOrder: 0
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0, y: 0}
  m_AnchorMax: {x: 1, y: 1}
  m_AnchoredPosition: {x: 0, y: 0}
  m_SizeDelta: {x: 0, y: 0}
  m_Pivot: {x: 0.5, y: 0.5}""")

    docs.append("""--- !u!114 &42
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 40}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {fileID: 0}
  m_Color: {r: 1, g: 1, b: 1, a: 0.005}
  m_RaycastTarget: 1
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {fileID: 0}
  m_Type: 0
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1""")

    docs.append("""--- !u!114 &43
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 40}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: %s
  m_Name: 
  m_EditorClassIdentifier: 
  Sensitivity: 1.05
  WorldCamera: {fileID: 0}""" % script_ref("Assets/Scripts/Gameplay/DragInput.cs"))

    # ---- EntityRoot(游戏实体渲染根) ----
    docs.append("""--- !u!1 &50
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 51}
  m_Layer: 5
  m_Name: EntityRoot
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1""")

    docs.append("""--- !u!224 &51
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 50}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_Children: []
  m_Father: {fileID: 31}
  m_RootOrder: 1
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0, y: 0}
  m_AnchorMax: {x: 0, y: 0}
  m_AnchoredPosition: {x: 0, y: 0}
  m_SizeDelta: {x: 900, y: 1600}
  m_Pivot: {x: 0, y: 0}""")

    # ---- FlashOverlay(受击/炸弹闪屏) ----
    docs.append("""--- !u!1 &55
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 56}
  - component: {fileID: 57}
  m_Layer: 5
  m_Name: FlashOverlay
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1""")

    docs.append("""--- !u!224 &56
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 55}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_Children: []
  m_Father: {fileID: 31}
  m_RootOrder: 2
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0, y: 0}
  m_AnchorMax: {x: 1, y: 1}
  m_AnchoredPosition: {x: 0, y: 0}
  m_SizeDelta: {x: 0, y: 0}
  m_Pivot: {x: 0.5, y: 0.5}""")

    docs.append("""--- !u!114 &57
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 55}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {fileID: 0}
  m_Color: {r: 1, g: 0.2, b: 0.15, a: 0}
  m_RaycastTarget: 0
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {fileID: 0}
  m_Type: 0
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1""")

    # ---- GameView ----
    docs.append("""--- !u!1 &60
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 61}
  - component: {fileID: 62}
  m_Layer: 0
  m_Name: GameView
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1""")

    docs.append("""--- !u!4 &61
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 60}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_Children: []
  m_Father: {fileID: 0}
  m_RootOrder: 3
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}""")

    docs.append("""--- !u!114 &62
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 60}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: %s
  m_Name: 
  m_EditorClassIdentifier: 
  OnlineMode: 0
  ServerUrl: ws://127.0.0.1:8080/
  Seed: 20260927
  WorldCamera: {fileID: 0}
  Drag: {fileID: 0}
  Ui: {fileID: 35}
  EntityRoot: {fileID: 51}
  FlashOverlay: {fileID: 57}""" % script_ref("Assets/Scripts/Rendering/GameView.cs"))

    # ---- Bootstrap ----
    docs.append("""--- !u!1 &70
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 71}
  - component: {fileID: 72}
  m_Layer: 0
  m_Name: Bootstrap
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1""")

    docs.append("""--- !u!4 &71
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 70}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_Children: []
  m_Father: {fileID: 0}
  m_RootOrder: 4
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}""")

    docs.append("""--- !u!114 &72
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 70}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: %s
  m_Name: 
  m_EditorClassIdentifier: 
  Mode: 0
  ServerUrl: ws://127.0.0.1:8080/
  Seed: 20260927""" % script_ref("Assets/Scripts/Gameplay/GameBootstrap.cs"))

    return "\n".join(docs) + "\n"


# ---------------------------------------------------------------------------
# ProjectSettings 生成
# ---------------------------------------------------------------------------

def axis(name, positive="", altPositive="", joyNum=0, axis=0, axis_type=0, gravity=0.0,
         dead=0.001, sensitivity=1.0, snap=False, invert=False, key=-1):
    return """  - serializedVersion: 3
    m_Name: %s
    descriptiveName: 
    descriptiveNegativeName: 
    negativeButton: 
    positiveButton: %s
    altNegativeButton: 
    altPositiveButton: %s
    gravity: %g
    dead: %g
    sensitivity: %g
    snap: %d
    invert: %d
    type: %d
    axis: %d
    joyNum: %d
""" % (name, positive, altPositive, gravity, dead, sensitivity, 1 if snap else 0,
       1 if invert else 0, axis_type, axis, joyNum)


def build_input_manager():
    axes = []
    axes.append(axis("Horizontal", "d", "right", axis_type=0, gravity=3, sensitivity=3, snap=True))
    axes.append(axis("Vertical", "w", "up", axis_type=0, gravity=3, sensitivity=3, snap=True))
    axes.append(axis("Fire1", "joystick button 0", axis_type=0, gravity=1000, sensitivity=1000))
    axes.append(axis("Fire2", "joystick button 1", axis_type=0, gravity=1000, sensitivity=1000))
    axes.append(axis("Fire3", "joystick button 2", axis_type=0, gravity=1000, sensitivity=1000))
    axes.append(axis("Jump", "space", axis_type=0, gravity=1000, sensitivity=1000))
    axes.append(axis("Mouse X", axis_type=1, axis=0, sensitivity=0.1))
    axes.append(axis("Mouse Y", axis_type=1, axis=1, sensitivity=0.1))
    axes.append(axis("Mouse ScrollWheel", axis_type=1, axis=2, sensitivity=0.1))
    axes.append(axis("Horizontal", axis_type=2, axis=0, joyNum=0, sensitivity=1, dead=0.19))
    axes.append(axis("Vertical", axis_type=2, axis=1, joyNum=0, sensitivity=1, dead=0.19, invert=True))
    axes.append(axis("Fire1", axis_type=0, joyNum=0, gravity=1000, sensitivity=1000))
    axes.append(axis("Fire2", axis_type=0, joyNum=0, gravity=1000, sensitivity=1000))
    axes.append(axis("Fire3", axis_type=0, joyNum=0, gravity=1000, sensitivity=1000))
    axes.append(axis("Jump", "joystick button 3", axis_type=0, gravity=1000, sensitivity=1000))
    axes.append(axis("Submit", "return", "joystick button 0", axis_type=0, gravity=1000, sensitivity=1000))
    axes.append(axis("Submit", "enter", "space", axis_type=0, gravity=1000, sensitivity=1000))
    axes.append(axis("Cancel", "escape", "joystick button 1", axis_type=0, gravity=1000, sensitivity=1000))
    return HEADER + """--- !u!13 &1
InputManager:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Axes:
""" + "".join(axes)


def build_project_settings(scene_guid):
    files = {}
    files["ProjectVersion.txt"] = "m_EditorVersion: 2019.4.40f1\n"
    files["InputManager.asset"] = build_input_manager()

    files["EditorBuildSettings.asset"] = HEADER + """--- !u!1045 &1
EditorBuildSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Scenes:
  - enabled: 1
    path: Assets/Scenes/Main.unity
    guid: %s
  m_configObjects: {}
""" % scene_guid

    files["TagManager.asset"] = HEADER + """--- !u!78 &1
TagManager:
  serializedVersion: 2
  tags: []
  layers:
  - Default
  - TransparentFX
  - Ignore Raycast
  - 
  - Water
  - UI
  - 
  - 
  m_SortingLayers:
  - name: Default
    uniqueID: 0
    locked: 0
"""

    files["TimeManager.asset"] = HEADER + """--- !u!5 &1
TimeManager:
  m_ObjectHideFlags: 0
  Fixed Timestep: 0.02
  Maximum Allowed Timestep: 0.33333334
  m_TimeScale: 1
  Maximum Particle Timestep: 0.03
"""

    files["AudioManager.asset"] = HEADER + """--- !u!11 &1
AudioManager:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Volume: 1
  Rolloff Scale: 1
  Doppler Factor: 1
  Default Speaker Mode: 2
  m_SampleRate: 0
  m_DSPBufferSize: 1024
  m_VirtualVoiceCount: 512
  m_RealVoiceCount: 32
  m_SpatializerPlugin: 
  m_AmbisonicDecoderPlugin: 
  m_DisableAudio: 0
  m_VirtualizeEffects: 1
  m_RequestedDSPBufferSize: 1024
"""

    files["ClusterInputManager.asset"] = HEADER + """--- !u!236 &1
ClusterInputManager:
  m_ObjectHideFlags: 0
  m_Inputs: []
"""

    files["DynamicsManager.asset"] = HEADER + """--- !u!55 &1
PhysicsManager:
  m_ObjectHideFlags: 0
  serializedVersion: 11
  m_Gravity: {x: 0, y: -9.81, z: 0}
  m_DefaultMaterial: {fileID: 0}
  m_BounceThreshold: 2
  m_SleepThreshold: 0.005
  m_DefaultContactOffset: 0.01
  m_DefaultSolverIterations: 6
  m_DefaultSolverVelocityIterations: 1
  m_QueriesHitBackfaces: 0
  m_QueriesHitTriggers: 1
  m_EnableAdaptiveForce: 0
  m_ClothInterCollisionDistance: 0
  m_ClothInterCollisionStiffness: 0
  m_ContactsGeneration: 1
  m_LayerCollisionMatrix: ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff
  m_AutoSimulation: 1
  m_AutoSyncTransforms: 0
  m_ReuseCollisionCallbacks: 1
  m_ClothInterCollisionSettingsToggle: 0
  m_ContactPairsMode: 0
  m_BroadphaseType: 0
  m_WorldBounds:
    m_Center: {x: 0, y: 0, z: 0}
    m_Extent: {x: 250, y: 250, z: 250}
  m_WorldSubdivisions: 8
  m_FrictionType: 0
  m_EnableEnhancedDeterminism: 0
  m_EnableUnifiedHeightmaps: 1
  m_DefaultMaxAngluarSpeed: 7
"""

    files["Physics2DSettings.asset"] = HEADER + """--- !u!19 &1
Physics2DSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 4
  m_Gravity: {x: 0, y: -9.81}
  m_DefaultMaterial: {fileID: 0}
  m_VelocityIterations: 8
  m_PositionIterations: 3
  m_VelocityThreshold: 1
  m_MaxLinearCorrection: 0.2
  m_MaxAngularCorrection: 8
  m_MaxTranslationSpeed: 100
  m_MaxRotationSpeed: 360
  m_BaumgarteScale: 0.2
  m_BaumgarteTimeOfImpactScale: 0.75
  m_TimeToSleep: 0.5
  m_LinearSleepTolerance: 0.01
  m_AngularSleepTolerance: 2
  m_DefaultContactOffset: 0.01
  m_JobOptions:
    serializedVersion: 2
    useMultithreading: 0
    useConsistencySorting: 0
    m_InterpolationPosesPerJob: 100
    m_NewContactsPerJob: 30
    m_CollideContactsPerJob: 100
    m_ClearFlagsPerJob: 200
    m_ClearBodyForcesPerJob: 200
    m_SyncDiscreteFixturesPerJob: 50
    m_SyncContinuousFixturesPerJob: 50
    m_FindNearestContactsPerJob: 100
    m_UpdateTriggerContactsPerJob: 100
    m_IslandSolverCostThreshold: 100
    m_IslandSolverBodyCostScale: 1
    m_IslandSolverContactCostScale: 10
    m_IslandSolverJointCostScale: 10
    m_IslandSolverBodiesPerJob: 50
    m_IslandSolverContactsPerJob: 50
  m_AutoSimulation: 1
  m_QueriesHitTriggers: 1
  m_QueriesStartInColliders: 1
  m_CallbacksOnDisable: 1
  m_ReuseCollisionCallbacks: 1
  m_AutoSyncTransforms: 0
  m_AlwaysShowColliders: 0
  m_ShowColliderSleep: 1
  m_ShowColliderContacts: 0
  m_ShowColliderAABB: 0
  m_ContactArrowScale: 0.2
  m_ColliderAwakeColor: {r: 0.5686275, g: 0.95686275, b: 0.54509807, a: 0.7529412}
  m_ColliderAsleepColor: {r: 0.5686275, g: 0.95686275, b: 0.54509807, a: 0.36078432}
  m_ColliderContactColor: {r: 1, g: 0, b: 1, a: 0.6862745}
  m_ColliderAABBColor: {r: 1, g: 1, b: 0, a: 0.2509804}
  m_LayerCollisionMatrix: ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff
"""

    files["NavMeshAreas.asset"] = HEADER + """--- !u!126 &1
NavMeshProjectSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  areas:
  - name: Walkable
    cost: 1
  - name: Not Walkable
    cost: 1
  m_LastAgentTypeID: -887442657
  m_Settings:
  - serializedVersion: 2
    agentTypeID: 0
    agentRadius: 0.5
    agentHeight: 2
    agentSlope: 45
    agentClimb: 0.75
    ledgeDropHeight: 0
    maxJumpAcrossDistance: 0
    minRegionArea: 2
    manualCellSize: 0
    cellSize: 0.16666667
    manualTileSize: 0
    tileSize: 256
    accuratePlacement: 0
    debug:
      m_Flags: 0
  m_SettingNames:
  - Humanoid
"""

    files["EditorSettings.asset"] = HEADER + """--- !u!159 &1
EditorSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 9
  m_ExternalVersionControlSupport: Visible Meta Files
  m_SerializationMode: 2
  m_LineEndingsForNewScripts: 0
  m_DefaultBehaviorMode: 1
  m_PrefabRegularEnvironment: {fileID: 0}
  m_PrefabUIEnvironment: {fileID: 0}
  m_SpritePackerMode: 4
  m_SpritePackerPaddingPower: 1
  m_EtcTextureCompressorBehavior: 1
  m_EtcTextureFastCompressor: 1
  m_EtcTextureNormalCompressor: 2
  m_EtcTextureBestCompressor: 4
  m_ProjectGenerationIncludedExtensions: txt;xml;fnt;cd;asmdef;rsp;asmref
  m_ProjectGenerationRootNamespace: 
  m_CollabEditorSettings:
    inProgressEnabled: 1
  m_EnableTextureStreamingInEditMode: 1
  m_EnableTextureStreamingInPlayMode: 1
  m_AsyncShaderCompilation: 1
  m_EnterPlayModeOptionsEnabled: 0
  m_EnterPlayModeOptions: 3
  m_ShowLightmapResolutionOverlay: 1
  m_UseLegacyProbeSampleCount: 0
  m_AssetPipelineMode: 1
  m_CacheServerMode: 0
  m_CacheServerEndpoint: 
  m_CacheServerNamespacePrefix: default
  m_CacheServerEnableDownload: 1
  m_CacheServerEnableUpload: 1
"""

    files["GraphicsSettings.asset"] = HEADER + """--- !u!30 &1
GraphicsSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 13
  m_Deferred:
    m_Mode: 1
    m_Shader: {fileID: 69, guid: 0000000000000000f000000000000000, type: 0}
  m_DeferredReflections:
    m_Mode: 1
    m_Shader: {fileID: 74, guid: 0000000000000000f000000000000000, type: 0}
  m_ScreenSpaceShadows:
    m_Mode: 1
    m_Shader: {fileID: 64, guid: 0000000000000000f000000000000000, type: 0}
  m_LegacyDeferred:
    m_Mode: 1
    m_Shader: {fileID: 63, guid: 0000000000000000f000000000000000, type: 0}
  m_DepthNormals:
    m_Mode: 1
    m_Shader: {fileID: 62, guid: 0000000000000000f000000000000000, type: 0}
  m_MotionVectors:
    m_Mode: 1
    m_Shader: {fileID: 75, guid: 0000000000000000f000000000000000, type: 0}
  m_LightHalo:
    m_Mode: 1
    m_Shader: {fileID: 105, guid: 0000000000000000f000000000000000, type: 0}
  m_LensFlare:
    m_Mode: 1
    m_Shader: {fileID: 102, guid: 0000000000000000f000000000000000, type: 0}
  m_AlwaysIncludedShaders:
  - {fileID: 7, guid: 0000000000000000f000000000000000, type: 0}
  - {fileID: 15104, guid: 0000000000000000f000000000000000, type: 0}
  - {fileID: 15105, guid: 0000000000000000f000000000000000, type: 0}
  - {fileID: 15106, guid: 0000000000000000f000000000000000, type: 0}
  - {fileID: 10753, guid: 0000000000000000f000000000000000, type: 0}
  - {fileID: 10770, guid: 0000000000000000f000000000000000, type: 0}
  m_PreloadedShaders: []
  m_SpritesDefaultMaterial: {fileID: 10754, guid: 0000000000000000f000000000000000,
    type: 0}
  m_CustomRenderPipeline: {fileID: 0}
  m_TransparencySortMode: 0
  m_TransparencySortAxis: {x: 0, y: 0, z: 1}
  m_DefaultRenderingPath: 1
  m_DefaultMobileRenderingPath: 1
  m_TierSettings: []
  m_LightmapStripping: 0
  m_FogStripping: 0
  m_InstancingStripping: 0
  m_LightmapKeepPlain: 1
  m_LightmapKeepDirCombined: 1
  m_LightmapKeepDynamicPlain: 1
  m_LightmapKeepDynamicDirCombined: 1
  m_LightmapKeepShadowMask: 1
  m_LightmapKeepSubtractive: 1
  m_FogKeepLinear: 1
  m_FogKeepExp: 1
  m_FogKeepExp2: 1
  m_AlbedoSwatchInfos: []
  m_LightsUseLinearIntensity: 0
  m_LightsUseColorTemperature: 0
  m_LogWhenShaderIsCompiled: 0
  m_AllowEnlightenSupportForUpgradedProject: 0
"""

    files["QualitySettings.asset"] = HEADER + """--- !u!47 &1
QualitySettings:
  m_ObjectHideFlags: 0
  serializedVersion: 5
  m_CurrentQuality: 0
  m_QualitySettings:
  - serializedVersion: 2
    name: High
    pixelLightCount: 2
    shadows: 0
    shadowResolution: 1
    shadowProjection: 1
    shadowCascades: 2
    shadowDistance: 40
    shadowNearPlaneOffset: 3
    shadowCascade2Split: 0.33333334
    shadowCascade4Split: {x: 0.06666667, y: 0.2, z: 0.46666667}
    shadowmaskMode: 1
    skinWeights: 2
    textureQuality: 0
    anisotropicTextures: 1
    antiAliasing: 0
    softParticles: 0
    softVegetation: 1
    realtimeReflectionProbes: 0
    billboardsFaceCameraPosition: 1
    vSyncCount: 1
    lodBias: 1
    maximumLODLevel: 0
    streamingMipmapsActive: 0
    streamingMipmapsAddAllCameras: 1
    streamingMipmapsMemoryBudget: 512
    streamingMipmapsRenderersPerFrame: 512
    streamingMipmapsMaxLevelReduction: 2
    streamingMipmapsMaxFileIORequests: 1024
    particleRaycastBudget: 256
    asyncUploadTimeSlice: 2
    asyncUploadBufferSize: 16
    asyncUploadPersistentBuffer: 1
    resolutionScalingFixedDPIFactor: 1
    customRenderPipeline: {fileID: 0}
    excludedTargetPlatforms: []
  m_PerPlatformDefaultQuality:
    Android: 0
    Lumin: 0
    Nintendo Switch: 0
    PS4: 0
    Stadia: 0
    Standalone: 0
    WebGL: 0
    Windows Store Apps: 0
    XboxOne: 0
    iPhone: 0
    tvOS: 0
"""

    files["UnityConnectSettings.asset"] = HEADER + """--- !u!310 &1
UnityConnectSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 1
  m_Enabled: 0
  m_TestMode: 0
  m_EventOldUrl: https://api.uca.cloud.unity3d.com/v1/events
  m_EventUrl: https://cdp.cloud.unity3d.com/v1/events
  m_ConfigUrl: https://config.uca.cloud.unity3d.com
  m_TestInitMode: 0
  CrashReportingSettings:
    m_EventUrl: https://perf-events.cloud.unity3d.com
    m_Enabled: 0
    m_LogBufferSize: 10
    m_CaptureEditorExceptions: 1
  UnityPurchasingSettings:
    m_Enabled: 0
    m_TestMode: 0
  UnityAnalyticsSettings:
    m_Enabled: 0
    m_TestMode: 0
    m_InitializeOnStartup: 1
  UnityAdsSettings:
    m_Enabled: 0
    m_InitializeOnStartup: 1
    m_TestMode: 0
    m_IosGameId: 
    m_AndroidGameId: 
    m_GameIds: {}
    m_GameId: 
  PerformanceReportingSettings:
    m_Enabled: 0
"""
    return files


GITIGNORE = """# Unity 生成目录
/[Ll]ibrary/
/[Tt]emp/
/[Oo]bj/
/[Bb]uild/
/[Bb]uilds/
/[Ll]ogs/
/[Uu]serSettings/
/[Mm]emoryCaptures/

# IDE
.vs/
.idea/
*.csproj
*.unityproj
*.sln
*.suo
*.userprefs
*.pidb
*.booproj
*.svd

# 运行时生成的贴图缓存(由 SpriteFactory 首次启动自动重建)
Assets/StreamingAssets/Sprites/

# OS
.DS_Store
Thumbs.db
"""


def main():
    # 场景与设置目录
    os.makedirs(os.path.join(ASSETS, "Scenes"), exist_ok=True)
    os.makedirs(os.path.join(ASSETS, "StreamingAssets"), exist_ok=True)
    os.makedirs(SETTINGS, exist_ok=True)

    # 场景文件(每次都重写,保持与脚本同步)
    with open(SCENE_PATH, "w", newline="\n") as f:
        f.write(build_scene())
    print("wrote", os.path.relpath(SCENE_PATH, ROOT))

    # meta
    gen_metas()
    print("meta files ensured")

    # ProjectSettings
    scene_guid = guid_for("Assets/Scenes/Main.unity")
    for name, content in build_project_settings(scene_guid).items():
        path = os.path.join(SETTINGS, name)
        with open(path, "w", newline="\n") as f:
            f.write(content)
        print("wrote", os.path.relpath(path, ROOT))

    # .gitignore
    gi = os.path.join(ROOT, ".gitignore")
    with open(gi, "w", newline="\n") as f:
        f.write(GITIGNORE)
    print("wrote .gitignore")

    # StreamingAssets 占位(保证目录入库;运行时缓存被 .gitignore 排除)
    keep = os.path.join(ASSETS, "StreamingAssets", ".gitkeep")
    if not os.path.exists(keep):
        with open(keep, "w") as f:
            f.write("")
    ensure_meta(keep, False)
    print("done")


if __name__ == "__main__":
    sys.exit(main())
