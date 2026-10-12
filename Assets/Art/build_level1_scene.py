#!/usr/bin/env python3
import os
import uuid
import math

SCENE_PATH = "Assets/Scenes/Level1.unity"

# All asset GUIDs
GUIDS = {
    # Scripts
    "GameController": "90a5a25a73e72449fb1f8de8e27008d7",
    "PlayerController": "61f259b3cdbce441996e9512b7eeebae",
    "GrapleController": "a7d7f3707fa5a4afeab7764c5199ff05",
    "ObjectVacum": "74f3bcd1977664930a4ec2a3d908341d",
    "PoolableController": "0e3f975a7f4bc41d6b168cb05932b95f",
    "RespawnController": "8a8ddc71bd8a245a697ea47d31187afb",
    "StarController": "0a95c6a505dbd41989ddfb26721a0131",
    "EnemyController": "f71469a4b47852b4a976e2bcadf0ddcd",
    "DeadlyHazard": "49824224b8955b1b8ae18447ede9c7aa",
    "HazardRotator": "109384d0e1cb5d95b58c27f3757667f6",
    "MovingPlatform": "4a0438fa0e2759348abf0c03d3fdc5f8",
    "GoalVictory": "2f1b3be866075c36a2cb068d2729a894",
    "PlayerHUD": "7f4c72557bf85824a36dd9d458cff309",
    "UniversalAdditionalCameraData": "a79441f348de89743a2939f4d699eac1",
    "Volume": "172515602e62fb746a5d573b38a5fe58",
    "TextMeshProUGUI": "f4688fdb7df04437aeb418b961361dc5",
    "CanvasScaler": "dc42784cf147c0c48a680349fa168899",
    "GraphicRaycaster": "dc42784cf147c0c48a680349fa168899",
    "InputSystemUIInputModule": "62883f63825c08147958060ec75b7726",

    # Prefabs & Assets
    "FirstPersonControllerPrefab": "c82236c16aae9d34fbd437f8dea6e2c9",
    "Sunset2Skybox": "528363c5c1cf6a7458140c6851c77b68",
    "SampleSceneProfile": "10fc4df2da32a41aaa32d77bc913491c",
    "LiberationSansSDF": "8f586378b4e144a9851e7b34d9b748ee",

    # Materials
    "Mat_AncientStone": "477aff47ff015f7bb07168f3324c6b6c",
    "Mat_RunicGold": "e7e2beebd3cd5f7db4771cdced6f8938",
    "Mat_GrappleCyan": "93ec656d8aa05292a03d42e0d75cef09",
    "Mat_EnergyCore": "55bf458817965f8aa2471b6e6c3c38cc",
    "Mat_HazardNeonCrimson": "5e649f83ac085020b4aa825ce7e29e75",
    "Mat_AcidToxic": "1cbc5578b0ef5c38a3791002ed99202d",
    "Mat_PortalVortex": "a95dabcc890a506dbf3fa3eecc992821",
    "Mat_StarGold": "9403a9f7e737563687b887f1b0a66c81",
    "Mat_CheckpointBeacon": "41c14746e4425204be56f745bb168eaf",
    "Mat_EnemyArmor": "245e6dadd3e555459f7b81bf04f4b1bf",
    "Mat_EnemyEye": "ac951a1f82245789b860c5c1c6060086",
    "ToolMat": "0e6d64548eaf8470199e90809979943e",

    # 3D Models
    "Model_HexPlatform": "4a76191c9ff752b0ad4534a6ef539db4",
    "Model_PathwayStone": "382c44933a0153868ec5d1b72a9e14ee",
    "Model_RoundStepping": "0a9bb5b28d095cf398c87f2ff841f38e",
    "Model_Obelisk": "8f831969a5e8557ea71e7c536551b943",
    "Model_GrappleAnchor": "93fba2c589fe5567b57bfdaeb15e966d",
    "Model_PlasmaSaw": "3d3e696f5b9b5f5492419ec6eb918f6f",
    "Model_SpikeTrap": "93122c608f6d543e8a4a581eebc9535b",
    "Model_VoidSentinel": "4e78287e07eb54d6afcc1ba95e4e2079",
    "Model_VictoryGate": "cb664b3c0a255ee88d229e6231d6837c",
    "Model_PowerCell": "7c0506e768e1544a8fb9a896aa339bb7",
    "Model_Checkpoint": "bf2d4ce0839e55728a4746e08a05c31d",
    "Model_StarRelic": "84aa59c86991583ea23efbafe589f783"
}

# Resolve model GUIDs from actual meta files
models_dir = "Assets/Art/Models"
for fname in os.listdir(models_dir):
    if fname.endswith(".obj.meta"):
        model_name = fname.replace(".obj.meta", "")
        with open(os.path.join(models_dir, fname)) as f:
            for line in f:
                if line.startswith("guid:"):
                    g = line.split(":")[1].strip()
                    GUIDS[f"Model_{model_name}"] = g

class SceneGenerator:
    def __init__(self):
        self.blocks = []
        self.next_id = 1000

    def alloc_id(self):
        self.next_id += 1
        return self.next_id

    def add_block(self, type_id, obj_id, content):
        self.blocks.append(f"--- !u!{type_id} &{obj_id}\n{content.strip()}\n")

    def create_game_object(self, name, layer=0, tag="Untagged", is_active=1):
        go_id = self.alloc_id()
        tr_id = self.alloc_id()
        return go_id, tr_id

def generate_scene():
    sg = SceneGenerator()

    # 1. Header / RenderSettings
    sg.add_block(29, 1, """
OcclusionCullingSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_OcclusionBakeSettings:
    smallestOccluder: 5
    smallestHole: 0.25
    backfaceThreshold: 100
  m_SceneGUID: 00000000000000000000000000000000
  m_OcclusionCullingData: {fileID: 0}
""")

    sg.add_block(104, 2, f"""
RenderSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 10
  m_Fog: 1
  m_FogColor: {{r: 0.85, g: 0.55, b: 0.45, a: 1}}
  m_FogMode: 3
  m_FogDensity: 0.003
  m_LinearFogStart: 0
  m_LinearFogEnd: 300
  m_AmbientSkyColor: {{r: 0.35, g: 0.3, b: 0.35, a: 1}}
  m_AmbientEquatorColor: {{r: 0.25, g: 0.2, b: 0.25, a: 1}}
  m_AmbientGroundColor: {{r: 0.15, g: 0.12, b: 0.15, a: 1}}
  m_AmbientIntensity: 1.1
  m_AmbientMode: 0
  m_SubtractiveShadowColor: {{r: 0.42, g: 0.478, b: 0.627, a: 1}}
  m_SkyboxMaterial: {{fileID: 2100000, guid: {GUIDS['Sunset2Skybox']}, type: 2}}
  m_HaloStrength: 0.5
  m_FlareStrength: 1
  m_FlareFadeSpeed: 3
  m_HaloTexture: {{fileID: 0}}
  m_SpotCookie: {{fileID: 10001, guid: 0000000000000000e000000000000000, type: 0}}
  m_DefaultReflectionMode: 0
  m_DefaultReflectionResolution: 128
  m_ReflectionBounces: 1
  m_ReflectionIntensity: 1
  m_CustomReflection: {{fileID: 0}}
  m_Sun: {{fileID: 0}}
  m_UseRadianceAmbientProbe: 0
""")

    sg.add_block(157, 3, """
LightmapSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 13
  m_BakeOnSceneLoad: 0
  m_GISettings:
    serializedVersion: 2
    m_BounceScale: 1
    m_IndirectOutputScale: 1
    m_AlbedoBoost: 1
    m_EnvironmentLightingMode: 0
    m_EnableBakedLightmaps: 1
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
    m_ReflectionCompression: 2
    m_MixedBakeMode: 2
    m_BakeBackend: 2
  m_LightingDataAsset: {fileID: 0}
  m_LightingSettings: {fileID: 0}
""")

    sg.add_block(196, 4, """
NavMeshSettings:
  serializedVersion: 2
  m_ObjectHideFlags: 0
  m_BuildSettings:
    serializedVersion: 3
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
    buildHeightMesh: 0
    maxJobWorkers: 0
    preserveTilesOutsideBounds: 0
    debug:
      m_Flags: 0
  m_NavMeshData: {fileID: 0}
""")

    # 2. Directional Sunlight
    sun_go = sg.alloc_id()
    sun_tr = sg.alloc_id()
    sun_lt = sg.alloc_id()
    sg.add_block(1, sun_go, f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {sun_tr}}}
  - component: {{fileID: {sun_lt}}}
  m_Layer: 0
  m_Name: Directional Light (Sunset)
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
    sg.add_block(4, sun_tr, f"""
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {sun_go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0.29883624, y: -0.3259881, z: 0.1098679, w: 0.89063776}}
  m_LocalPosition: {{x: 0, y: 30, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 37, y: -40, z: 0}}
""")
    sg.add_block(108, sun_lt, f"""
Light:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {sun_go}}}
  m_Enabled: 1
  serializedVersion: 11
  m_Type: 1
  m_Color: {{r: 1, g: 0.88, b: 0.74, a: 1}}
  m_Intensity: 1.25
  m_Range: 10
  m_SpotAngle: 30
  m_InnerSpotAngle: 21.80208
  m_CookieSize: 10
  m_Shadows:
    m_Type: 2
    m_Resolution: -1
    m_CustomResolution: -1
    m_Strength: 0.85
    m_Bias: 0.05
    m_NormalBias: 0.4
    m_NearPlane: 0.2
    m_CullingMatrixOverride:
      e00: 1
      e01: 0
      e02: 0
      e03: 0
      e10: 0
      e11: 1
      e12: 0
      e13: 0
      e20: 0
      e21: 0
      e22: 1
      e23: 0
      e30: 0
      e31: 0
      e32: 0
      e33: 1
    m_UseCullingMatrixOverride: 0
  m_Cookie: {{fileID: 0}}
  m_DrawHalo: 0
  m_Flare: {{fileID: 0}}
  m_RenderMode: 0
  m_CullingMask:
    serializedVersion: 2
    m_Bits: 4294967295
  m_RenderingLayerMask: 1
  m_Lightmapping: 4
  m_LightShadowCasterMode: 0
  m_AreaSize: {{x: 1, y: 1}}
  m_BounceIntensity: 1
  m_ColorTemperature: 6570
  m_UseColorTemperature: 0
  m_BoundingSphereOverride: {{x: 0, y: 0, z: 0, w: 0}}
  m_UseBoundingSphereOverride: 0
  m_UseViewFrustumForShadowCasterCull: 1
  m_ForceVisible: 0
  m_ShadowRadius: 0
  m_ShadowAngle: 0
  m_LightUnit: 1
  m_LuxAtDistance: 1
  m_EnableSpotReflector: 1
""")

    # 3. Global Volume (Bloom, Tonemapping, Vignette)
    vol_go = sg.alloc_id()
    vol_tr = sg.alloc_id()
    vol_mb = sg.alloc_id()
    sg.add_block(1, vol_go, f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {vol_tr}}}
  - component: {{fileID: {vol_mb}}}
  m_Layer: 0
  m_Name: Global PostProcessing Volume
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
    sg.add_block(4, vol_tr, f"""
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {vol_go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
""")
    sg.add_block(114, vol_mb, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {vol_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUIDS['Volume']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Unity.RenderPipelines.Core.Runtime::UnityEngine.Rendering.Volume
  m_IsGlobal: 1
  priority: 0
  blendDistance: 0
  weight: 1
  sharedProfile: {{fileID: 11400000, guid: {GUIDS['SampleSceneProfile']}, type: 2}}
""")

    # 4. Game Manager (GM)
    gm_go = sg.alloc_id()
    gm_tr = sg.alloc_id()
    gm_mb = sg.alloc_id()

    # Pre-allocate UI IDs
    canvas_go = sg.alloc_id()
    canvas_rt = sg.alloc_id()
    canvas_c = sg.alloc_id()
    canvas_cs = sg.alloc_id()
    canvas_gr = sg.alloc_id()
    hud_mb = sg.alloc_id()

    starscore_go = sg.alloc_id()
    starscore_rt = sg.alloc_id()
    starscore_tmp = sg.alloc_id()

    hud_tool_go = sg.alloc_id()
    hud_tool_rt = sg.alloc_id()
    hud_tool_tmp = sg.alloc_id()

    hud_hp_go = sg.alloc_id()
    hud_hp_rt = sg.alloc_id()
    hud_hp_tmp = sg.alloc_id()

    victory_go = sg.alloc_id()
    victory_rt = sg.alloc_id()
    victory_title_go = sg.alloc_id()
    victory_title_rt = sg.alloc_id()
    victory_title_tmp = sg.alloc_id()
    victory_stars_go = sg.alloc_id()
    victory_stars_rt = sg.alloc_id()
    victory_stars_tmp = sg.alloc_id()
    victory_time_go = sg.alloc_id()
    victory_time_rt = sg.alloc_id()
    victory_time_tmp = sg.alloc_id()
    victory_btn_go = sg.alloc_id()
    victory_btn_rt = sg.alloc_id()
    victory_btn_img = sg.alloc_id()
    victory_btn_b = sg.alloc_id()
    victory_btn_txt_go = sg.alloc_id()
    victory_btn_txt_rt = sg.alloc_id()
    victory_btn_txt_tmp = sg.alloc_id()

    goal_victory_mb = sg.alloc_id()

    # GM setup
    sg.add_block(1, gm_go, f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {gm_tr}}}
  - component: {{fileID: {gm_mb}}}
  m_Layer: 0
  m_Name: GM
  m_TagString: GameController
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
    sg.add_block(4, gm_tr, f"""
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {gm_go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
""")
    sg.add_block(114, gm_mb, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {gm_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUIDS['GameController']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::GameController
  scoreText: {{fileID: {starscore_tmp}}}
""")

    # 5. EventSystem
    es_go = sg.alloc_id()
    es_tr = sg.alloc_id()
    es_mb1 = sg.alloc_id()
    es_mb2 = sg.alloc_id()
    sg.add_block(1, es_go, f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {es_tr}}}
  - component: {{fileID: {es_mb1}}}
  - component: {{fileID: {es_mb2}}}
  m_Layer: 0
  m_Name: EventSystem
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
    sg.add_block(4, es_tr, f"""
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {es_go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
""")
    sg.add_block(114, es_mb1, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {es_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: 76bf8d588d87ac94583f237c1b300e82, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.EventSystems.EventSystem
  m_FirstSelected: {{fileID: 0}}
  m_sendNavigationEvents: 1
  m_DragThreshold: 10
""")
    sg.add_block(114, es_mb2, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {es_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUIDS['InputSystemUIInputModule']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Unity.InputSystem::UnityEngine.InputSystem.UI.InputSystemUIInputModule
  m_ActionsAsset: {{fileID: -944628639613478452, guid: 052faaac586de48259a63d0c4782560b, type: 3}}
""")

    # 6. Canvas & UI
    sg.add_block(1, canvas_go, f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {canvas_rt}}}
  - component: {{fileID: {canvas_c}}}
  - component: {{fileID: {canvas_cs}}}
  - component: {{fileID: {canvas_gr}}}
  - component: {{fileID: {hud_mb}}}
  m_Layer: 5
  m_Name: HUD Canvas
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
    sg.add_block(224, canvas_rt, f"""
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {canvas_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 0, y: 0, z: 0}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {starscore_rt}}}
  - {{fileID: {hud_tool_rt}}}
  - {{fileID: {hud_hp_rt}}}
  - {{fileID: {victory_rt}}}
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 0, y: 0}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0, y: 0}}
""")
    sg.add_block(223, canvas_c, f"""
Canvas:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {canvas_go}}}
  m_Enabled: 1
  serializedVersion: 3
  m_RenderMode: 0
  m_Camera: {{fileID: 0}}
  m_PlaneDistance: 100
  m_PixelPerfect: 0
  m_ReceivesEvents: 1
  m_OverrideSorting: 0
  m_OverridePixelPerfect: 0
  m_SortingBucketNormalizedSize: 0
  m_VertexColorAlwaysGammaSpace: 0
  m_AdditionalShaderChannelsFlag: 25
  m_UpdateRectTransformForStandalone: 0
  m_SortingLayerID: 0
  m_SortingOrder: 10
  m_TargetDisplay: 0
""")
    sg.add_block(114, canvas_cs, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {canvas_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: dc42784cf147c0c48a680349fa168899, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.CanvasScaler
  m_UiScaleMode: 1
  m_ReferencePixelsPerUnit: 100
  m_ScaleFactor: 1
  m_ReferenceResolution: {{x: 1920, y: 1080}}
  m_ScreenMatchMode: 0
  m_MatchWidthOrHeight: 0.5
  m_PhysicalUnit: 3
  m_FallbackScreenDPI: 96
  m_DefaultSpriteDPI: 96
  m_DynamicPixelsPerUnit: 1
  m_PresetInfoIsWorld: 0
""")
    sg.add_block(114, canvas_gr, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {canvas_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: 0cd44c1031e13a943bb63640046fad76, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.GraphicRaycaster
  m_IgnoreReversedGraphics: 1
  m_BlockingObjects: 0
  m_BlockingMask:
    serializedVersion: 2
    m_Bits: 4294967295
""")
    sg.add_block(114, hud_mb, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {canvas_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUIDS['PlayerHUD']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::PlayerHUD
  playerController: {{fileID: 0}}
  gameController: {{fileID: {gm_mb}}}
  healthHearts: []
  healthText: {{fileID: {hud_hp_tmp}}}
  toolNameText: {{fileID: {hud_tool_tmp}}}
  toolIconImage: {{fileID: 0}}
  grappleColor: {{r: 1, g: 0.55, b: 0.15, a: 1}}
  vacuumColor: {{r: 0.15, g: 0.85, b: 1, a: 1}}
  starsText: {{fileID: {starscore_tmp}}}
""")

    # StarScore Text
    sg.add_block(1, starscore_go, f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {starscore_rt}}}
  - component: {{fileID: {starscore_tmp}}}
  m_Layer: 5
  m_Name: StarScore
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
    sg.add_block(224, starscore_rt, f"""
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {starscore_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {canvas_rt}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 1, y: 1}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: -160, y: -50}}
  m_SizeDelta: {{x: 280, y: 60}}
  m_Pivot: {{x: 1, y: 1}}
""")
    sg.add_block(114, starscore_tmp, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {starscore_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUIDS['TextMeshProUGUI']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Unity.TextMeshPro::TMPro.TextMeshProUGUI
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 0.9, b: 0.2, a: 1}}
  m_RaycastTarget: 0
  m_text: '★ Stars: 0/3'
  m_fontSize: 32
  m_fontSizeBase: 32
  m_fontWeight: 600
  m_enableWordWrapping: 0
  m_alignment: 514
  m_fontAsset: {{fileID: 11400000, guid: {GUIDS['LiberationSansSDF']}, type: 2}}
""")

    # Tool HUD Text
    sg.add_block(1, hud_tool_go, f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {hud_tool_rt}}}
  - component: {{fileID: {hud_tool_tmp}}}
  m_Layer: 5
  m_Name: ToolHUD
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
    sg.add_block(224, hud_tool_rt, f"""
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {hud_tool_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {canvas_rt}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0.5, y: 0}}
  m_AnchorMax: {{x: 0.5, y: 0}}
  m_AnchoredPosition: {{x: 0, y: 40}}
  m_SizeDelta: {{x: 800, y: 50}}
  m_Pivot: {{x: 0.5, y: 0}}
""")
    sg.add_block(114, hud_tool_tmp, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {hud_tool_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUIDS['TextMeshProUGUI']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Unity.TextMeshPro::TMPro.TextMeshProUGUI
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_RaycastTarget: 0
  m_text: 'HERRAMIENTA: GANCHO (X / Q)'
  m_fontSize: 24
  m_fontSizeBase: 24
  m_fontWeight: 500
  m_enableWordWrapping: 0
  m_alignment: 514
  m_fontAsset: {{fileID: 11400000, guid: {GUIDS['LiberationSansSDF']}, type: 2}}
""")

    # Health HUD Text
    sg.add_block(1, hud_hp_go, f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {hud_hp_rt}}}
  - component: {{fileID: {hud_hp_tmp}}}
  m_Layer: 5
  m_Name: HealthHUD
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
    sg.add_block(224, hud_hp_rt, f"""
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {hud_hp_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {canvas_rt}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 1}}
  m_AnchorMax: {{x: 0, y: 1}}
  m_AnchoredPosition: {{x: 50, y: -50}}
  m_SizeDelta: {{x: 260, y: 60}}
  m_Pivot: {{x: 0, y: 1}}
""")
    sg.add_block(114, hud_hp_tmp, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {hud_hp_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUIDS['TextMeshProUGUI']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Unity.TextMeshPro::TMPro.TextMeshProUGUI
  m_Material: {{fileID: 0}}
  m_Color: {{r: 0.2, g: 1, b: 0.4, a: 1}}
  m_RaycastTarget: 0
  m_text: 'HP: 3 / 3'
  m_fontSize: 32
  m_fontSizeBase: 32
  m_fontWeight: 600
  m_enableWordWrapping: 0
  m_alignment: 513
  m_fontAsset: {{fileID: 11400000, guid: {GUIDS['LiberationSansSDF']}, type: 2}}
""")

    # Victory Modal Panel
    victory_img = sg.alloc_id()
    sg.add_block(1, victory_go, f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {victory_rt}}}
  - component: {{fileID: {victory_img}}}
  m_Layer: 5
  m_Name: VictoryModal
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 0
""")
    sg.add_block(224, victory_rt, f"""
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {victory_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {victory_title_rt}}}
  - {{fileID: {victory_stars_rt}}}
  - {{fileID: {victory_time_rt}}}
  - {{fileID: {victory_btn_rt}}}
  m_Father: {{fileID: {canvas_rt}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0.5, y: 0.5}}
  m_AnchorMax: {{x: 0.5, y: 0.5}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 640, y: 440}}
  m_Pivot: {{x: 0.5, y: 0.5}}
""")
    sg.add_block(114, victory_img, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {victory_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.Image
  m_Material: {{fileID: 0}}
  m_Color: {{r: 0.08, g: 0.09, b: 0.14, a: 0.95}}
  m_RaycastTarget: 1
""")

    # Victory Title
    sg.add_block(1, victory_title_go, f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {victory_title_rt}}}
  - component: {{fileID: {victory_title_tmp}}}
  m_Layer: 5
  m_Name: VictoryTitle
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
    sg.add_block(224, victory_title_rt, f"""
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {victory_title_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {victory_rt}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0.5, y: 1}}
  m_AnchorMax: {{x: 0.5, y: 1}}
  m_AnchoredPosition: {{x: 0, y: -60}}
  m_SizeDelta: {{x: 580, y: 70}}
  m_Pivot: {{x: 0.5, y: 1}}
""")
    sg.add_block(114, victory_title_tmp, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {victory_title_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUIDS['TextMeshProUGUI']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Unity.TextMeshPro::TMPro.TextMeshProUGUI
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 0.85, b: 0.2, a: 1}}
  m_RaycastTarget: 0
  m_text: '¡NIVEL 1 COMPLETADO!'
  m_fontSize: 38
  m_fontSizeBase: 38
  m_fontWeight: 700
  m_enableWordWrapping: 0
  m_alignment: 514
  m_fontAsset: {{fileID: 11400000, guid: {GUIDS['LiberationSansSDF']}, type: 2}}
""")

    # Victory Stars
    sg.add_block(1, victory_stars_go, f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {victory_stars_rt}}}
  - component: {{fileID: {victory_stars_tmp}}}
  m_Layer: 5
  m_Name: VictoryStars
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
    sg.add_block(224, victory_stars_rt, f"""
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {victory_stars_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {victory_rt}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0.5, y: 1}}
  m_AnchorMax: {{x: 0.5, y: 1}}
  m_AnchoredPosition: {{x: 0, y: -150}}
  m_SizeDelta: {{x: 500, y: 50}}
  m_Pivot: {{x: 0.5, y: 1}}
""")
    sg.add_block(114, victory_stars_tmp, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {victory_stars_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUIDS['TextMeshProUGUI']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Unity.TextMeshPro::TMPro.TextMeshProUGUI
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_RaycastTarget: 0
  m_text: 'Estrellas Conseguidas: 3/3'
  m_fontSize: 26
  m_fontSizeBase: 26
  m_fontWeight: 500
  m_enableWordWrapping: 0
  m_alignment: 514
  m_fontAsset: {{fileID: 11400000, guid: {GUIDS['LiberationSansSDF']}, type: 2}}
""")

    # Victory Time
    sg.add_block(1, victory_time_go, f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {victory_time_rt}}}
  - component: {{fileID: {victory_time_tmp}}}
  m_Layer: 5
  m_Name: VictoryTime
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
    sg.add_block(224, victory_time_rt, f"""
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {victory_time_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {victory_rt}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0.5, y: 1}}
  m_AnchorMax: {{x: 0.5, y: 1}}
  m_AnchoredPosition: {{x: 0, y: -210}}
  m_SizeDelta: {{x: 500, y: 50}}
  m_Pivot: {{x: 0.5, y: 1}}
""")
    sg.add_block(114, victory_time_tmp, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {victory_time_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUIDS['TextMeshProUGUI']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Unity.TextMeshPro::TMPro.TextMeshProUGUI
  m_Material: {{fileID: 0}}
  m_Color: {{r: 0.6, g: 0.8, b: 1, a: 1}}
  m_RaycastTarget: 0
  m_text: '¡Enhorabuena, has triunfado!'
  m_fontSize: 22
  m_fontSizeBase: 22
  m_fontWeight: 400
  m_enableWordWrapping: 0
  m_alignment: 514
  m_fontAsset: {{fileID: 11400000, guid: {GUIDS['LiberationSansSDF']}, type: 2}}
""")

    # Victory Restart Button
    sg.add_block(1, victory_btn_go, f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {victory_btn_rt}}}
  - component: {{fileID: {victory_btn_img}}}
  - component: {{fileID: {victory_btn_b}}}
  m_Layer: 5
  m_Name: RestartButton
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
    sg.add_block(224, victory_btn_rt, f"""
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {victory_btn_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {victory_btn_txt_rt}}}
  m_Father: {{fileID: {victory_rt}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0.5, y: 0}}
  m_AnchorMax: {{x: 0.5, y: 0}}
  m_AnchoredPosition: {{x: 0, y: 50}}
  m_SizeDelta: {{x: 280, y: 65}}
  m_Pivot: {{x: 0.5, y: 0}}
""")
    sg.add_block(114, victory_btn_img, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {victory_btn_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.Image
  m_Material: {{fileID: 0}}
  m_Color: {{r: 0.15, g: 0.65, b: 0.95, a: 1}}
  m_RaycastTarget: 1
""")
    sg.add_block(114, victory_btn_b, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {victory_btn_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: 4e29b1a8efbd4b44bb3f3716e73f07ff, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.Button
  m_Interactable: 1
  m_TargetGraphic: {{fileID: {victory_btn_img}}}
  m_OnClick:
    m_PersistentCalls:
      m_Calls:
      - m_Target: {{fileID: {goal_victory_mb}}}
        m_TargetAssemblyTypeName: GoalVictory, Assembly-CSharp
        m_MethodName: RestartLevel
        m_Mode: 1
        m_Arguments:
          m_ObjectArgument: {{fileID: 0}}
          m_ObjectArgumentAssemblyTypeName: UnityEngine.Object, UnityEngine
          m_IntArgument: 0
          m_FloatArgument: 0
          m_StringArgument: 
          m_BoolArgument: 0
        m_CallState: 2
""")
    sg.add_block(1, victory_btn_txt_go, f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {victory_btn_txt_rt}}}
  - component: {{fileID: {victory_btn_txt_tmp}}}
  m_Layer: 5
  m_Name: Text
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
    sg.add_block(224, victory_btn_txt_rt, f"""
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {victory_btn_txt_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {victory_btn_rt}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
""")
    sg.add_block(114, victory_btn_txt_tmp, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {victory_btn_txt_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUIDS['TextMeshProUGUI']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Unity.TextMeshPro::TMPro.TextMeshProUGUI
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_RaycastTarget: 0
  m_text: 'REINTENTAR'
  m_fontSize: 24
  m_fontSizeBase: 24
  m_fontWeight: 600
  m_enableWordWrapping: 0
  m_alignment: 514
  m_fontAsset: {{fileID: 11400000, guid: {GUIDS['LiberationSansSDF']}, type: 2}}
""")

    # 7. Player (PrefabInstance)
    player_pi = 560267824
    player_go_stripped = 560267825
    cam_go_stripped = 560267826
    player_tr_stripped = 560267827
    player_pc_mb = 560267828
    player_cam_mb = 560267829

    tool_go = 1048108835
    tool_tr = 1048108836
    tool_vac_mb = 1048108837
    tool_col = 1048108838
    tool_mr = 1048108839
    tool_mf = 1048108840
    tool_grap_mb = 1048108841

    sg.add_block(1001, player_pi, f"""
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {{fileID: 0}}
    m_Modifications:
    - target: {{fileID: 9156347144040445544, guid: {GUIDS['FirstPersonControllerPrefab']}, type: 3}}
      propertyPath: jumpSpeed
      value: 6.5
      objectReference: {{fileID: 0}}
    - target: {{fileID: 9156347144040445544, guid: {GUIDS['FirstPersonControllerPrefab']}, type: 3}}
      propertyPath: walkSpeed
      value: 5.5
      objectReference: {{fileID: 0}}
    - target: {{fileID: 9156347144040445544, guid: {GUIDS['FirstPersonControllerPrefab']}, type: 3}}
      propertyPath: sprintSpeed
      value: 8.5
      objectReference: {{fileID: 0}}
    - target: {{fileID: 9156347144040445546, guid: {GUIDS['FirstPersonControllerPrefab']}, type: 3}}
      propertyPath: m_LocalPosition.x
      value: 0
      objectReference: {{fileID: 0}}
    - target: {{fileID: 9156347144040445546, guid: {GUIDS['FirstPersonControllerPrefab']}, type: 3}}
      propertyPath: m_LocalPosition.y
      value: 1.5
      objectReference: {{fileID: 0}}
    - target: {{fileID: 9156347144040445546, guid: {GUIDS['FirstPersonControllerPrefab']}, type: 3}}
      propertyPath: m_LocalPosition.z
      value: 0
      objectReference: {{fileID: 0}}
    - target: {{fileID: 9156347144040445547, guid: {GUIDS['FirstPersonControllerPrefab']}, type: 3}}
      propertyPath: m_Name
      value: FirstPersonController
      objectReference: {{fileID: 0}}
    - target: {{fileID: 9156347144040445547, guid: {GUIDS['FirstPersonControllerPrefab']}, type: 3}}
      propertyPath: m_TagString
      value: Player
      objectReference: {{fileID: 0}}
    m_RemovedComponents: []
    m_RemovedGameObjects: []
    m_AddedGameObjects:
    - targetCorrespondingSourceObject: {{fileID: 9156347144040445546, guid: {GUIDS['FirstPersonControllerPrefab']}, type: 3}}
      insertIndex: -1
      addedObject: {{fileID: {tool_tr}}}
    m_AddedComponents:
    - targetCorrespondingSourceObject: {{fileID: 9156347144040445547, guid: {GUIDS['FirstPersonControllerPrefab']}, type: 3}}
      insertIndex: -1
      addedObject: {{fileID: {player_pc_mb}}}
    - targetCorrespondingSourceObject: {{fileID: 9156347143994207886, guid: {GUIDS['FirstPersonControllerPrefab']}, type: 3}}
      insertIndex: -1
      addedObject: {{fileID: {player_cam_mb}}}
  m_SourcePrefab: {{fileID: 100100000, guid: {GUIDS['FirstPersonControllerPrefab']}, type: 3}}
""")

    sg.blocks.append(f"""--- !u!1 &{player_go_stripped} stripped
GameObject:
  m_CorrespondingSourceObject: {{fileID: 9156347144040445547, guid: {GUIDS['FirstPersonControllerPrefab']}, type: 3}}
  m_PrefabInstance: {{fileID: {player_pi}}}
  m_PrefabAsset: {{fileID: 0}}
""")
    sg.blocks.append(f"""--- !u!1 &{cam_go_stripped} stripped
GameObject:
  m_CorrespondingSourceObject: {{fileID: 9156347143994207886, guid: {GUIDS['FirstPersonControllerPrefab']}, type: 3}}
  m_PrefabInstance: {{fileID: {player_pi}}}
  m_PrefabAsset: {{fileID: 0}}
""")
    sg.blocks.append(f"""--- !u!4 &{player_tr_stripped} stripped
Transform:
  m_CorrespondingSourceObject: {{fileID: 9156347144040445546, guid: {GUIDS['FirstPersonControllerPrefab']}, type: 3}}
  m_PrefabInstance: {{fileID: {player_pi}}}
  m_PrefabAsset: {{fileID: 0}}
""")

    sg.add_block(114, player_pc_mb, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {player_go_stripped}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUIDS['PlayerController']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::PlayerController
  vacuum: {{fileID: {tool_vac_mb}}}
  graple: {{fileID: {tool_grap_mb}}}
  selectedTool: 0
  vacuumHoldTime: 0.2
  toolStick: {{fileID: 0}}
  dead: 0
  health: 3
""")

    sg.add_block(114, player_cam_mb, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {cam_go_stripped}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUIDS['UniversalAdditionalCameraData']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Unity.RenderPipelines.Universal.Runtime::UnityEngine.Rendering.Universal.UniversalAdditionalCameraData
  m_RenderShadows: 1
  m_RequiresDepthTextureOption: 2
  m_RequiresOpaqueTextureOption: 2
  m_CameraType: 0
  m_Cameras: []
  m_RendererIndex: -1
  m_VolumeLayerMask:
    serializedVersion: 2
    m_Bits: 1
  m_VolumeTrigger: {{fileID: 0}}
  m_VolumeFrameworkUpdateModeOption: 2
  m_RenderPostProcessing: 1
  m_Antialiasing: 1
  m_AntialiasingQuality: 2
""")

    # Tool Child
    sg.add_block(1, tool_go, f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {tool_tr}}}
  - component: {{fileID: {tool_mf}}}
  - component: {{fileID: {tool_mr}}}
  - component: {{fileID: {tool_col}}}
  - component: {{fileID: {tool_vac_mb}}}
  - component: {{fileID: {tool_grap_mb}}}
  m_Layer: 0
  m_Name: Tool
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
    sg.add_block(4, tool_tr, f"""
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {tool_go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0.35, y: -0.3, z: 0.65}}
  m_LocalScale: {{x: 0.22, y: 0.05, z: 0.46}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {player_tr_stripped}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
""")
    sg.add_block(33, tool_mf, f"""
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {tool_go}}}
  m_Mesh: {{fileID: 10208, guid: 0000000000000000e000000000000000, type: 0}}
""")
    sg.add_block(23, tool_mr, f"""
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {tool_go}}}
  m_Enabled: 1
  m_CastShadows: 1
  m_ReceiveShadows: 1
  m_Materials:
  - {{fileID: 2100000, guid: {GUIDS['ToolMat']}, type: 2}}
""")
    sg.add_block(135, tool_col, f"""
SphereCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {tool_go}}}
  m_Material: {{fileID: 0}}
  m_IsTrigger: 1
  m_Radius: 0.5
  m_Center: {{x: 0, y: 0, z: 0}}
""")
    sg.add_block(114, tool_vac_mb, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {tool_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUIDS['ObjectVacum']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::ObjectVacum
  vacuumActive: 0
  maxDistance: 12
  firstPersonController: {{fileID: 0}}
  launchImpulse: 50
  launchClearance: 0.2
  launchObstacleMask:
    serializedVersion: 2
    m_Bits: 4294967295
  launchSpin: 2
  maxLaunchSpin: 5
  objectCapacity: 5
""")
    sg.add_block(114, tool_grap_mb, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {tool_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUIDS['GrapleController']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::GrapleController
  maxDistance: 60
  arrivalRadius: 1.5
  maxPullDuration: 3
  overshootDistance: 2
  gravity: 0
  firstPersonController: {{fileID: 0}}
  grappleMask:
    serializedVersion: 2
    m_Bits: 507
  pullAcceleration: 38
  pullSpeed: 24
  airControl: 8
  releaseMomentumRetention: 0.85
  launchLift: 4
  pullRampTime: 0.2
""")

    # Helper function to add a standard 3D static environment prop with model and material
    def add_model_prop(name, model_guid, mat_guid, pos, rot=(0,0,0), scale=(1,1,1), layer=0, box_col=None):
        go_id = sg.alloc_id()
        tr_id = sg.alloc_id()
        mf_id = sg.alloc_id()
        mr_id = sg.alloc_id()
        components = [f"- component: {{fileID: {tr_id}}}", f"- component: {{fileID: {mf_id}}}", f"- component: {{fileID: {mr_id}}}"]
        col_id = None
        if box_col:
            col_id = sg.alloc_id()
            components.append(f"- component: {{fileID: {col_id}}}")

        comp_str = "\n  ".join(components)
        sg.add_block(1, go_id, f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  {comp_str}
  m_Layer: {layer}
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
        # Euler to Quaternion conversion
        rx, ry, rz = math.radians(rot[0]), math.radians(rot[1]), math.radians(rot[2])
        cy, sy = math.cos(ry*0.5), math.sin(ry*0.5)
        cp, sp = math.cos(rx*0.5), math.sin(rx*0.5)
        cr, sr = math.cos(rz*0.5), math.sin(rz*0.5)
        qw = cr * cp * cy + sr * sp * sy
        qx = sr * cp * cy - cr * sp * sy
        qy = cr * sp * cy + sr * cp * sy
        qz = cr * cp * sy - sr * sp * cy

        sg.add_block(4, tr_id, f"""
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  serializedVersion: 2
  m_LocalRotation: {{x: {qx:.5f}, y: {qy:.5f}, z: {qz:.5f}, w: {qw:.5f}}}
  m_LocalPosition: {{x: {pos[0]:.3f}, y: {pos[1]:.3f}, z: {pos[2]:.3f}}}
  m_LocalScale: {{x: {scale[0]:.3f}, y: {scale[1]:.3f}, z: {scale[2]:.3f}}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: {rot[0]}, y: {rot[1]}, z: {rot[2]}}}
""")
        sg.add_block(33, mf_id, f"""
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Mesh: {{fileID: 4300000, guid: {model_guid}, type: 3}}
""")
        sg.add_block(23, mr_id, f"""
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  m_CastShadows: 1
  m_ReceiveShadows: 1
  m_Materials:
  - {{fileID: 2100000, guid: {mat_guid}, type: 2}}
""")
        if box_col:
            cx, cy, cz = box_col.get('center', (0,0,0))
            sx, sy, sz = box_col.get('size', (1,1,1))
            is_trig = 1 if box_col.get('is_trigger', False) else 0
            sg.add_block(65, col_id, f"""
BoxCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Material: {{fileID: 0}}
  m_IsTrigger: {is_trig}
  m_Size: {{x: {sx:.3f}, y: {sy:.3f}, z: {sz:.3f}}}
  m_Center: {{x: {cx:.3f}, y: {cy:.3f}, z: {cz:.3f}}}
""")
        return go_id, tr_id

    # 8. Add Void Killzone (DeadlyHazard)
    void_go = sg.alloc_id()
    void_tr = sg.alloc_id()
    void_col = sg.alloc_id()
    void_haz = sg.alloc_id()
    sg.add_block(1, void_go, f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {void_tr}}}
  - component: {{fileID: {void_col}}}
  - component: {{fileID: {void_haz}}}
  m_Layer: 0
  m_Name: Void KillZone (DeadlyHazard)
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
    sg.add_block(4, void_tr, f"""
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {void_go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: -15, z: 80}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
""")
    sg.add_block(65, void_col, f"""
BoxCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {void_go}}}
  m_Material: {{fileID: 0}}
  m_IsTrigger: 1
  m_Size: {{x: 200, y: 5, z: 260}}
  m_Center: {{x: 0, y: 0, z: 0}}
""")
    sg.add_block(114, void_haz, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {void_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUIDS['DeadlyHazard']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::DeadlyHazard
  hazardType: 0
  damageAmount: 99
  damageInterval: 1
  knockbackForce: 0
""")

    # 9. Zone 1: Sanctuary of Awakening
    # Spawn platform
    add_model_prop("Sanctuary_Hex_Spawn", GUIDS['Model_Platform_SanctuaryHex'], GUIDS['Mat_AncientStone'], (0, 0, 0), scale=(1.4, 1.2, 1.4), box_col={'center':(0,0,0), 'size':(12, 1.4, 12)})
    # Columns around spawn
    add_model_prop("Obelisk_1", GUIDS['Model_Pillar_CelestialObelisk'], GUIDS['Mat_RunicGold'], (-5.5, 0.5, -4), scale=(1, 1.3, 1), box_col={'center':(0,2.5,0), 'size':(1.6, 5.5, 1.6)})
    add_model_prop("Obelisk_2", GUIDS['Model_Pillar_CelestialObelisk'], GUIDS['Mat_RunicGold'], (5.5, 0.5, -4), scale=(1, 1.3, 1), box_col={'center':(0,2.5,0), 'size':(1.6, 5.5, 1.6)})
    add_model_prop("Obelisk_3", GUIDS['Model_Pillar_CelestialObelisk'], GUIDS['Mat_AncientStone'], (-5.5, 0.5, 4), scale=(1, 1.3, 1), box_col={'center':(0,2.5,0), 'size':(1.6, 5.5, 1.6)})
    add_model_prop("Obelisk_4", GUIDS['Model_Pillar_CelestialObelisk'], GUIDS['Mat_AncientStone'], (5.5, 0.5, 4), scale=(1, 1.3, 1), box_col={'center':(0,2.5,0), 'size':(1.6, 5.5, 1.6)})

    # Stepping stones leading forward
    add_model_prop("Step_Stone_1", GUIDS['Model_Platform_PathwayStone'], GUIDS['Mat_AncientStone'], (0, 0.3, 8), scale=(1, 1, 1), box_col={'center':(0,0,0), 'size':(7, 1.6, 5)})
    add_model_prop("Step_Round_2", GUIDS['Model_Platform_RoundStepping'], GUIDS['Mat_RunicGold'], (2.5, 0.9, 14), scale=(1.2, 1.2, 1.2), box_col={'center':(0,0,0), 'size':(4.5, 1, 4.5)})
    add_model_prop("Step_Stone_3", GUIDS['Model_Platform_PathwayStone'], GUIDS['Mat_AncientStone'], (0, 1.5, 20), scale=(1, 1, 1), box_col={'center':(0,0,0), 'size':(7, 1.6, 5)})

    # Star 1 Collectible
    star1_go = sg.alloc_id()
    star1_tr = sg.alloc_id()
    star1_mf = sg.alloc_id()
    star1_mr = sg.alloc_id()
    star1_col = sg.alloc_id()
    star1_sc = sg.alloc_id()
    sg.add_block(1, star1_go, f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {star1_tr}}}
  - component: {{fileID: {star1_mf}}}
  - component: {{fileID: {star1_mr}}}
  - component: {{fileID: {star1_col}}}
  - component: {{fileID: {star1_sc}}}
  m_Layer: 0
  m_Name: Star 1 (Collectible)
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
    sg.add_block(4, star1_tr, f"""
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {star1_go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 3.2, z: 20}}
  m_LocalScale: {{x: 1.5, y: 1.5, z: 1.5}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
""")
    sg.add_block(33, star1_mf, f"""
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {star1_go}}}
  m_Mesh: {{fileID: 4300000, guid: {GUIDS['Model_Star_Relic3D']}, type: 3}}
""")
    sg.add_block(23, star1_mr, f"""
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {star1_go}}}
  m_Enabled: 1
  m_CastShadows: 1
  m_ReceiveShadows: 1
  m_Materials:
  - {{fileID: 2100000, guid: {GUIDS['Mat_StarGold']}, type: 2}}
""")
    sg.add_block(135, star1_col, f"""
SphereCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {star1_go}}}
  m_Material: {{fileID: 0}}
  m_IsTrigger: 1
  m_Radius: 0.8
  m_Center: {{x: 0, y: 0, z: 0}}
""")
    sg.add_block(114, star1_sc, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {star1_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUIDS['StarController']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::StarController
""")

    # Checkpoint 1
    cp1_go = sg.alloc_id()
    cp1_tr = sg.alloc_id()
    cp1_mf = sg.alloc_id()
    cp1_mr = sg.alloc_id()
    cp1_col = sg.alloc_id()
    cp1_rc = sg.alloc_id()
    add_model_prop("CP1_Platform", GUIDS['Model_Platform_RoundStepping'], GUIDS['Mat_AncientStone'], (0, 1.8, 26), scale=(1.4, 1.2, 1.4), box_col={'center':(0,0,0), 'size':(5, 1, 5)})
    sg.add_block(1, cp1_go, f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {cp1_tr}}}
  - component: {{fileID: {cp1_mf}}}
  - component: {{fileID: {cp1_mr}}}
  - component: {{fileID: {cp1_col}}}
  - component: {{fileID: {cp1_rc}}}
  m_Layer: 0
  m_Name: Checkpoint 1 (Beacon)
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
    sg.add_block(4, cp1_tr, f"""
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {cp1_go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 2.3, z: 26}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
""")
    sg.add_block(33, cp1_mf, f"""
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {cp1_go}}}
  m_Mesh: {{fileID: 4300000, guid: {GUIDS['Model_Checkpoint_Monolith']}, type: 3}}
""")
    sg.add_block(23, cp1_mr, f"""
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {cp1_go}}}
  m_Enabled: 1
  m_CastShadows: 1
  m_ReceiveShadows: 1
  m_Materials:
  - {{fileID: 2100000, guid: {GUIDS['Mat_CheckpointBeacon']}, type: 2}}
""")
    sg.add_block(65, cp1_col, f"""
BoxCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {cp1_go}}}
  m_Material: {{fileID: 0}}
  m_IsTrigger: 1
  m_Size: {{x: 2.5, y: 3.5, z: 2.5}}
  m_Center: {{x: 0, y: 1.5, z: 0}}
""")
    sg.add_block(114, cp1_rc, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {cp1_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUIDS['RespawnController']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::RespawnController
  beaconRenderer: {{fileID: {cp1_mr}}}
  activeColor: {{r: 0.2, g: 1, b: 0.4, a: 1}}
""")

    # 10. Zone 2: The Grapple Abyss
    # 3 Grapple Anchors on Layer 8 (Graple)
    def add_grapple_anchor(name, pos):
        go_id = sg.alloc_id()
        tr_id = sg.alloc_id()
        mf_id = sg.alloc_id()
        mr_id = sg.alloc_id()
        col_id = sg.alloc_id()
        rot_id = sg.alloc_id()
        sg.add_block(1, go_id, f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {tr_id}}}
  - component: {{fileID: {mf_id}}}
  - component: {{fileID: {mr_id}}}
  - component: {{fileID: {col_id}}}
  - component: {{fileID: {rot_id}}}
  m_Layer: 8
  m_Name: {name} (Grapple Anchor)
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
        sg.add_block(4, tr_id, f"""
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: {pos[0]:.3f}, y: {pos[1]:.3f}, z: {pos[2]:.3f}}}
  m_LocalScale: {{x: 1.3, y: 1.3, z: 1.3}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
""")
        sg.add_block(33, mf_id, f"""
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Mesh: {{fileID: 4300000, guid: {GUIDS['Model_Grapple_AnchorSphere']}, type: 3}}
""")
        sg.add_block(23, mr_id, f"""
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  m_CastShadows: 1
  m_ReceiveShadows: 1
  m_Materials:
  - {{fileID: 2100000, guid: {GUIDS['Mat_GrappleCyan']}, type: 2}}
""")
        sg.add_block(135, col_id, f"""
SphereCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Material: {{fileID: 0}}
  m_IsTrigger: 0
  m_Radius: 1.5
  m_Center: {{x: 0, y: 0, z: 0}}
""")
        sg.add_block(114, rot_id, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUIDS['HazardRotator']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::HazardRotator
  rotationSpeed: {{x: 15, y: 45, z: 20}}
  oscillate: 0
""")

    add_grapple_anchor("Grapple_Anchor_1", (0, 7.5, 36))
    add_grapple_anchor("Grapple_Anchor_2", (5.5, 9.5, 47))
    add_grapple_anchor("Grapple_Anchor_3", (-3.5, 8.5, 57))

    # Star 2 High in the Grapple Arc
    star2_go = sg.alloc_id()
    star2_tr = sg.alloc_id()
    star2_mf = sg.alloc_id()
    star2_mr = sg.alloc_id()
    star2_col = sg.alloc_id()
    star2_sc = sg.alloc_id()
    sg.add_block(1, star2_go, f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {star2_tr}}}
  - component: {{fileID: {star2_mf}}}
  - component: {{fileID: {star2_mr}}}
  - component: {{fileID: {star2_col}}}
  - component: {{fileID: {star2_sc}}}
  m_Layer: 0
  m_Name: Star 2 (High Grapple Secret)
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
    sg.add_block(4, star2_tr, f"""
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {star2_go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 5.5, y: 13.5, z: 47}}
  m_LocalScale: {{x: 1.5, y: 1.5, z: 1.5}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
""")
    sg.add_block(33, star2_mf, f"""
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {star2_go}}}
  m_Mesh: {{fileID: 4300000, guid: {GUIDS['Model_Star_Relic3D']}, type: 3}}
""")
    sg.add_block(23, star2_mr, f"""
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {star2_go}}}
  m_Enabled: 1
  m_CastShadows: 1
  m_ReceiveShadows: 1
  m_Materials:
  - {{fileID: 2100000, guid: {GUIDS['Mat_StarGold']}, type: 2}}
""")
    sg.add_block(135, star2_col, f"""
SphereCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {star2_go}}}
  m_Material: {{fileID: 0}}
  m_IsTrigger: 1
  m_Radius: 0.8
  m_Center: {{x: 0, y: 0, z: 0}}
""")
    sg.add_block(114, star2_sc, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {star2_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUIDS['StarController']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::StarController
""")

    # Landing Platform after Grapple Abyss
    add_model_prop("Landing_Hex_PostGrapple", GUIDS['Model_Platform_SanctuaryHex'], GUIDS['Mat_AncientStone'], (0, 3.0, 67), scale=(1.3, 1.2, 1.3), box_col={'center':(0,0,0), 'size':(11, 1.4, 11)})

    # Checkpoint 2
    cp2_go = sg.alloc_id()
    cp2_tr = sg.alloc_id()
    cp2_mf = sg.alloc_id()
    cp2_mr = sg.alloc_id()
    cp2_col = sg.alloc_id()
    cp2_rc = sg.alloc_id()
    sg.add_block(1, cp2_go, f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {cp2_tr}}}
  - component: {{fileID: {cp2_mf}}}
  - component: {{fileID: {cp2_mr}}}
  - component: {{fileID: {cp2_col}}}
  - component: {{fileID: {cp2_rc}}}
  m_Layer: 0
  m_Name: Checkpoint 2 (Post-Grapple)
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
    sg.add_block(4, cp2_tr, f"""
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {cp2_go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 3.5, z: 67}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
""")
    sg.add_block(33, cp2_mf, f"""
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {cp2_go}}}
  m_Mesh: {{fileID: 4300000, guid: {GUIDS['Model_Checkpoint_Monolith']}, type: 3}}
""")
    sg.add_block(23, cp2_mr, f"""
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {cp2_go}}}
  m_Enabled: 1
  m_CastShadows: 1
  m_ReceiveShadows: 1
  m_Materials:
  - {{fileID: 2100000, guid: {GUIDS['Mat_CheckpointBeacon']}, type: 2}}
""")
    sg.add_block(65, cp2_col, f"""
BoxCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {cp2_go}}}
  m_Material: {{fileID: 0}}
  m_IsTrigger: 1
  m_Size: {{x: 2.5, y: 3.5, z: 2.5}}
  m_Center: {{x: 0, y: 1.5, z: 0}}
""")
    sg.add_block(114, cp2_rc, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {cp2_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUIDS['RespawnController']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::RespawnController
  beaconRenderer: {{fileID: {cp2_mr}}}
  activeColor: {{r: 0.2, g: 1, b: 0.4, a: 1}}
""")

    # 11. Zone 3: The Sentinel Arena (Vacuum & Enemies)
    # Grand Arena Floor
    add_model_prop("Arena_Main_Platform", GUIDS['Model_Platform_SanctuaryHex'], GUIDS['Mat_AncientStone'], (0, 3.2, 83), scale=(2.4, 1.4, 2.4), box_col={'center':(0,0,0), 'size':(22, 1.6, 22)})

    # Decorative pillars enclosing the arena
    add_model_prop("Arena_Pillar_1", GUIDS['Model_Pillar_CelestialObelisk'], GUIDS['Mat_AncientStone'], (-8.5, 3.8, 77), scale=(1, 1.4, 1), box_col={'center':(0,2.5,0), 'size':(1.6, 6, 1.6)})
    add_model_prop("Arena_Pillar_2", GUIDS['Model_Pillar_CelestialObelisk'], GUIDS['Mat_AncientStone'], (8.5, 3.8, 77), scale=(1, 1.4, 1), box_col={'center':(0,2.5,0), 'size':(1.6, 6, 1.6)})
    add_model_prop("Arena_Pillar_3", GUIDS['Model_Pillar_CelestialObelisk'], GUIDS['Mat_AncientStone'], (-8.5, 3.8, 89), scale=(1, 1.4, 1), box_col={'center':(0,2.5,0), 'size':(1.6, 6, 1.6)})
    add_model_prop("Arena_Pillar_4", GUIDS['Model_Pillar_CelestialObelisk'], GUIDS['Mat_AncientStone'], (8.5, 3.8, 89), scale=(1, 1.4, 1), box_col={'center':(0,2.5,0), 'size':(1.6, 6, 1.6)})

    # Central Rotating Hazard: Plasma Saw
    saw_go = sg.alloc_id()
    saw_tr = sg.alloc_id()
    saw_mf = sg.alloc_id()
    saw_mr = sg.alloc_id()
    saw_col = sg.alloc_id()
    saw_rot = sg.alloc_id()
    saw_haz = sg.alloc_id()
    sg.add_block(1, saw_go, f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {saw_tr}}}
  - component: {{fileID: {saw_mf}}}
  - component: {{fileID: {saw_mr}}}
  - component: {{fileID: {saw_col}}}
  - component: {{fileID: {saw_rot}}}
  - component: {{fileID: {saw_haz}}}
  m_Layer: 0
  m_Name: Hazard_PlasmaSaw_Center
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
    sg.add_block(4, saw_tr, f"""
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {saw_go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 4.5, z: 83}}
  m_LocalScale: {{x: 1.4, y: 1.2, z: 1.4}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
""")
    sg.add_block(33, saw_mf, f"""
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {saw_go}}}
  m_Mesh: {{fileID: 4300000, guid: {GUIDS['Model_Hazard_PlasmaSaw']}, type: 3}}
""")
    sg.add_block(23, saw_mr, f"""
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {saw_go}}}
  m_Enabled: 1
  m_CastShadows: 1
  m_ReceiveShadows: 1
  m_Materials:
  - {{fileID: 2100000, guid: {GUIDS['Mat_HazardNeonCrimson']}, type: 2}}
""")
    sg.add_block(135, saw_col, f"""
SphereCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {saw_go}}}
  m_Material: {{fileID: 0}}
  m_IsTrigger: 1
  m_Radius: 2.5
  m_Center: {{x: 0, y: 0, z: 0}}
""")
    sg.add_block(114, saw_rot, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {saw_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUIDS['HazardRotator']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::HazardRotator
  rotationSpeed: {{x: 0, y: 130, z: 0}}
  oscillate: 0
""")
    sg.add_block(114, saw_haz, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {saw_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUIDS['DeadlyHazard']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::DeadlyHazard
  hazardType: 1
  damageAmount: 1
  damageInterval: 0.8
  knockbackForce: 8
""")

    # 4 Vacuum Ammo Cells (Layer 7: Poolable)
    def add_poolable_cell(name, pos):
        go_id = sg.alloc_id()
        tr_id = sg.alloc_id()
        mf_id = sg.alloc_id()
        mr_id = sg.alloc_id()
        rb_id = sg.alloc_id()
        col_id = sg.alloc_id()
        pool_id = sg.alloc_id()
        sg.add_block(1, go_id, f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {tr_id}}}
  - component: {{fileID: {mf_id}}}
  - component: {{fileID: {mr_id}}}
  - component: {{fileID: {rb_id}}}
  - component: {{fileID: {col_id}}}
  - component: {{fileID: {pool_id}}}
  m_Layer: 7
  m_Name: {name} (Poolable Energy Cell)
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
        sg.add_block(4, tr_id, f"""
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: {pos[0]:.3f}, y: {pos[1]:.3f}, z: {pos[2]:.3f}}}
  m_LocalScale: {{x: 1.1, y: 1.1, z: 1.1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
""")
        sg.add_block(33, mf_id, f"""
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Mesh: {{fileID: 4300000, guid: {GUIDS['Model_Prop_PowerCell']}, type: 3}}
""")
        sg.add_block(23, mr_id, f"""
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  m_CastShadows: 1
  m_ReceiveShadows: 1
  m_Materials:
  - {{fileID: 2100000, guid: {GUIDS['Mat_EnergyCore']}, type: 2}}
""")
        sg.add_block(54, rb_id, f"""
Rigidbody:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  serializedVersion: 4
  m_Mass: 1
  m_LinearDamping: 0.1
  m_AngularDamping: 0.2
  m_CenterOfMass: {{x: 0, y: 0, z: 0}}
  m_InertiaTensor: {{x: 1, y: 1, z: 1}}
  m_InertiaRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ImplicitCom: 1
  m_ImplicitTensor: 1
  m_UseGravity: 1
  m_IsKinematic: 0
  m_Interpolate: 1
  m_Constraints: 0
  m_CollisionDetection: 0
""")
        sg.add_block(135, col_id, f"""
SphereCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Material: {{fileID: 0}}
  m_IsTrigger: 0
  m_Radius: 0.5
  m_Center: {{x: 0, y: 0, z: 0}}
""")
        sg.add_block(114, pool_id, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUIDS['PoolableController']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::PoolableController
  absorptionDuration: 0.75
  spiralRadius: 0.4
  spiralTurns: 2
  scaleRestoreDuration: 0.2
  absorptionDistance: 0.3
""")

    add_poolable_cell("EnergyCell_1", (-5.5, 4.5, 78))
    add_poolable_cell("EnergyCell_2", (5.5, 4.5, 78))
    add_poolable_cell("EnergyCell_3", (-5.5, 4.5, 88))
    add_poolable_cell("EnergyCell_4", (5.5, 4.5, 88))

    # Basic Enemies: Corrupted Void Sentinels
    def add_void_sentinel(name, pos):
        go_id = sg.alloc_id()
        tr_id = sg.alloc_id()
        mf_id = sg.alloc_id()
        mr_id = sg.alloc_id()
        col_id = sg.alloc_id()
        rb_id = sg.alloc_id()
        ai_id = sg.alloc_id()

        sg.add_block(1, go_id, f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {tr_id}}}
  - component: {{fileID: {mf_id}}}
  - component: {{fileID: {mr_id}}}
  - component: {{fileID: {col_id}}}
  - component: {{fileID: {rb_id}}}
  - component: {{fileID: {ai_id}}}
  m_Layer: 0
  m_Name: {name} (Enemy Sentinel)
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
        sg.add_block(4, tr_id, f"""
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: {pos[0]:.3f}, y: {pos[1]:.3f}, z: {pos[2]:.3f}}}
  m_LocalScale: {{x: 1.2, y: 1.2, z: 1.2}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
""")
        sg.add_block(33, mf_id, f"""
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Mesh: {{fileID: 4300000, guid: {GUIDS['Model_Enemy_VoidSentinel']}, type: 3}}
""")
        sg.add_block(23, mr_id, f"""
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  m_CastShadows: 1
  m_ReceiveShadows: 1
  m_Materials:
  - {{fileID: 2100000, guid: {GUIDS['Mat_EnemyEye']}, type: 2}}
""")
        sg.add_block(135, col_id, f"""
SphereCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Material: {{fileID: 0}}
  m_IsTrigger: 0
  m_Radius: 0.9
  m_Center: {{x: 0, y: 0, z: 0}}
""")
        sg.add_block(54, rb_id, f"""
Rigidbody:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  serializedVersion: 4
  m_Mass: 3
  m_LinearDamping: 0.5
  m_AngularDamping: 0.8
  m_UseGravity: 0
  m_IsKinematic: 0
  m_Constraints: 112
""")
        sg.add_block(114, ai_id, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUIDS['EnemyController']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::EnemyController
  detectionRange: 16
  attackRange: 2.4
  chaseSpeed: 4.5
  turnSpeed: 7
  patrolRadius: 5
  attackDamage: 1
  attackCooldown: 1.6
  maxHealth: 1
  glowRenderer: {{fileID: {mr_id}}}
  normalGlowColor: {{r: 0.8, g: 0.2, b: 0.9, a: 1}}
  alertGlowColor: {{r: 1, g: 0.1, b: 0.1, a: 1}}
""")

    add_void_sentinel("Sentinel_Alpha", (-4.0, 4.8, 83))
    add_void_sentinel("Sentinel_Beta", (4.0, 4.8, 85))

    # Pathway exit & Checkpoint 3
    add_model_prop("Arena_Exit_Path", GUIDS['Model_Platform_PathwayStone'], GUIDS['Mat_AncientStone'], (0, 3.2, 97), scale=(1, 1, 1), box_col={'center':(0,0,0), 'size':(7, 1.6, 5)})
    cp3_go = sg.alloc_id()
    cp3_tr = sg.alloc_id()
    cp3_mf = sg.alloc_id()
    cp3_mr = sg.alloc_id()
    cp3_col = sg.alloc_id()
    cp3_rc = sg.alloc_id()
    sg.add_block(1, cp3_go, f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {cp3_tr}}}
  - component: {{fileID: {cp3_mf}}}
  - component: {{fileID: {cp3_mr}}}
  - component: {{fileID: {cp3_col}}}
  - component: {{fileID: {cp3_rc}}}
  m_Layer: 0
  m_Name: Checkpoint 3 (Post-Arena)
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
    sg.add_block(4, cp3_tr, f"""
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {cp3_go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 4.0, z: 101}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
""")
    sg.add_block(33, cp3_mf, f"""
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {cp3_go}}}
  m_Mesh: {{fileID: 4300000, guid: {GUIDS['Model_Checkpoint_Monolith']}, type: 3}}
""")
    sg.add_block(23, cp3_mr, f"""
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {cp3_go}}}
  m_Enabled: 1
  m_CastShadows: 1
  m_ReceiveShadows: 1
  m_Materials:
  - {{fileID: 2100000, guid: {GUIDS['Mat_CheckpointBeacon']}, type: 2}}
""")
    sg.add_block(65, cp3_col, f"""
BoxCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {cp3_go}}}
  m_Material: {{fileID: 0}}
  m_IsTrigger: 1
  m_Size: {{x: 2.5, y: 3.5, z: 2.5}}
  m_Center: {{x: 0, y: 1.5, z: 0}}
""")
    sg.add_block(114, cp3_rc, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {cp3_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUIDS['RespawnController']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::RespawnController
  beaconRenderer: {{fileID: {cp3_mr}}}
  activeColor: {{r: 0.2, g: 1, b: 0.4, a: 1}}
""")

    # 12. Zone 4: The Celestial Ascent & Moving Obstacles
    # Moving Platform 1 (oscillates horizontally)
    mov1_go = sg.alloc_id()
    mov1_tr = sg.alloc_id()
    mov1_mf = sg.alloc_id()
    mov1_mr = sg.alloc_id()
    mov1_col = sg.alloc_id()
    mov1_mb = sg.alloc_id()
    sg.add_block(1, mov1_go, f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {mov1_tr}}}
  - component: {{fileID: {mov1_mf}}}
  - component: {{fileID: {mov1_mr}}}
  - component: {{fileID: {mov1_col}}}
  - component: {{fileID: {mov1_mb}}}
  m_Layer: 0
  m_Name: Moving_Platform_1
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
    sg.add_block(4, mov1_tr, f"""
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {mov1_go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 5.0, z: 109}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
""")
    sg.add_block(33, mov1_mf, f"""
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {mov1_go}}}
  m_Mesh: {{fileID: 4300000, guid: {GUIDS['Model_Platform_PathwayStone']}, type: 3}}
""")
    sg.add_block(23, mov1_mr, f"""
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {mov1_go}}}
  m_Enabled: 1
  m_CastShadows: 1
  m_ReceiveShadows: 1
  m_Materials:
  - {{fileID: 2100000, guid: {GUIDS['Mat_AncientStone']}, type: 2}}
""")
    sg.add_block(65, mov1_col, f"""
BoxCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {mov1_go}}}
  m_Material: {{fileID: 0}}
  m_IsTrigger: 0
  m_Size: {{x: 7, y: 1.6, z: 5}}
  m_Center: {{x: 0, y: 0, z: 0}}
""")
    sg.add_block(114, mov1_mb, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {mov1_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUIDS['MovingPlatform']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::MovingPlatform
  moveOffset: {{x: 6, y: 0, z: 0}}
  speed: 1.4
""")

    # Spiked Hazard Stepping Stone
    add_model_prop("Spike_Step_Platform", GUIDS['Model_Platform_RoundStepping'], GUIDS['Mat_AncientStone'], (3, 6.5, 118), scale=(1.3, 1.2, 1.3), box_col={'center':(0,0,0), 'size':(4.8, 1, 4.8)})
    spk_go = sg.alloc_id()
    spk_tr = sg.alloc_id()
    spk_mf = sg.alloc_id()
    spk_mr = sg.alloc_id()
    spk_col = sg.alloc_id()
    spk_haz = sg.alloc_id()
    sg.add_block(1, spk_go, f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {spk_tr}}}
  - component: {{fileID: {spk_mf}}}
  - component: {{fileID: {spk_mr}}}
  - component: {{fileID: {spk_col}}}
  - component: {{fileID: {spk_haz}}}
  m_Layer: 0
  m_Name: Hazard_SpikeTrap
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
    sg.add_block(4, spk_tr, f"""
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {spk_go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 3, y: 7.0, z: 118}}
  m_LocalScale: {{x: 1.2, y: 1.2, z: 1.2}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
""")
    sg.add_block(33, spk_mf, f"""
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {spk_go}}}
  m_Mesh: {{fileID: 4300000, guid: {GUIDS['Model_Hazard_SpikeTrap']}, type: 3}}
""")
    sg.add_block(23, spk_mr, f"""
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {spk_go}}}
  m_Enabled: 1
  m_CastShadows: 1
  m_ReceiveShadows: 1
  m_Materials:
  - {{fileID: 2100000, guid: {GUIDS['Mat_HazardNeonCrimson']}, type: 2}}
""")
    sg.add_block(65, spk_col, f"""
BoxCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {spk_go}}}
  m_Material: {{fileID: 0}}
  m_IsTrigger: 1
  m_Size: {{x: 2.2, y: 2.0, z: 2.2}}
  m_Center: {{x: 0, y: 0.8, z: 0}}
""")
    sg.add_block(114, spk_haz, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {spk_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUIDS['DeadlyHazard']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::DeadlyHazard
  hazardType: 2
  damageAmount: 1
  damageInterval: 1
  knockbackForce: 10
""")

    # Moving Platform 2 (oscillates forward-upward)
    mov2_go = sg.alloc_id()
    mov2_tr = sg.alloc_id()
    mov2_mf = sg.alloc_id()
    mov2_mr = sg.alloc_id()
    mov2_col = sg.alloc_id()
    mov2_mb = sg.alloc_id()
    sg.add_block(1, mov2_go, f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {mov2_tr}}}
  - component: {{fileID: {mov2_mf}}}
  - component: {{fileID: {mov2_mr}}}
  - component: {{fileID: {mov2_col}}}
  - component: {{fileID: {mov2_mb}}}
  m_Layer: 0
  m_Name: Moving_Platform_2
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
    sg.add_block(4, mov2_tr, f"""
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {mov2_go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 7.8, z: 126}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
""")
    sg.add_block(33, mov2_mf, f"""
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {mov2_go}}}
  m_Mesh: {{fileID: 4300000, guid: {GUIDS['Model_Platform_PathwayStone']}, type: 3}}
""")
    sg.add_block(23, mov2_mr, f"""
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {mov2_go}}}
  m_Enabled: 1
  m_CastShadows: 1
  m_ReceiveShadows: 1
  m_Materials:
  - {{fileID: 2100000, guid: {GUIDS['Mat_AncientStone']}, type: 2}}
""")
    sg.add_block(65, mov2_col, f"""
BoxCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {mov2_go}}}
  m_Material: {{fileID: 0}}
  m_IsTrigger: 0
  m_Size: {{x: 7, y: 1.6, z: 5}}
  m_Center: {{x: 0, y: 0, z: 0}}
""")
    sg.add_block(114, mov2_mb, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {mov2_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUIDS['MovingPlatform']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::MovingPlatform
  moveOffset: {{x: -4, y: 2.5, z: 6}}
  speed: 1.2
""")

    # High Grapple Anchor to Summit
    add_grapple_anchor("Summit_Grapple_Anchor", (0, 15.0, 138))

    # Star 3 Atop the High Grapple Anchor
    star3_go = sg.alloc_id()
    star3_tr = sg.alloc_id()
    star3_mf = sg.alloc_id()
    star3_mr = sg.alloc_id()
    star3_col = sg.alloc_id()
    star3_sc = sg.alloc_id()
    sg.add_block(1, star3_go, f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {star3_tr}}}
  - component: {{fileID: {star3_mf}}}
  - component: {{fileID: {star3_mr}}}
  - component: {{fileID: {star3_col}}}
  - component: {{fileID: {star3_sc}}}
  m_Layer: 0
  m_Name: Star 3 (Summit Apex)
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
    sg.add_block(4, star3_tr, f"""
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {star3_go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 17.5, z: 138}}
  m_LocalScale: {{x: 1.5, y: 1.5, z: 1.5}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
""")
    sg.add_block(33, star3_mf, f"""
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {star3_go}}}
  m_Mesh: {{fileID: 4300000, guid: {GUIDS['Model_Star_Relic3D']}, type: 3}}
""")
    sg.add_block(23, star3_mr, f"""
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {star3_go}}}
  m_Enabled: 1
  m_CastShadows: 1
  m_ReceiveShadows: 1
  m_Materials:
  - {{fileID: 2100000, guid: {GUIDS['Mat_StarGold']}, type: 2}}
""")
    sg.add_block(135, star3_col, f"""
SphereCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {star3_go}}}
  m_Material: {{fileID: 0}}
  m_IsTrigger: 1
  m_Radius: 0.8
  m_Center: {{x: 0, y: 0, z: 0}}
""")
    sg.add_block(114, star3_sc, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {star3_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUIDS['StarController']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::StarController
""")

    # Guardian Sentinel guarding the summit approach
    add_void_sentinel("Sentinel_Guardian", (0, 12.0, 146))

    # 13. Zone 5: The Celestial Victory Shrine & Summit (Meta / Victoria)
    # Grand Summit Platform
    add_model_prop("Summit_Hex_Platform", GUIDS['Model_Platform_SanctuaryHex'], GUIDS['Mat_AncientStone'], (0, 10.5, 156), scale=(2.0, 1.4, 2.0), box_col={'center':(0,0,0), 'size':(18, 1.6, 18)})
    add_model_prop("Summit_Obelisk_L", GUIDS['Model_Pillar_CelestialObelisk'], GUIDS['Mat_RunicGold'], (-6, 11.2, 154), scale=(1.2, 1.6, 1.2), box_col={'center':(0,2.5,0), 'size':(1.8, 7, 1.8)})
    add_model_prop("Summit_Obelisk_R", GUIDS['Model_Pillar_CelestialObelisk'], GUIDS['Mat_RunicGold'], (6, 11.2, 154), scale=(1.2, 1.6, 1.2), box_col={'center':(0,2.5,0), 'size':(1.8, 7, 1.8)})

    # The Grand Celestial Victory Gate (Goal)
    gate_go = sg.alloc_id()
    gate_tr = sg.alloc_id()
    gate_mf = sg.alloc_id()
    gate_mr = sg.alloc_id()
    gate_col = sg.alloc_id()
    gate_lt = sg.alloc_id()
    sg.add_block(1, gate_go, f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {gate_tr}}}
  - component: {{fileID: {gate_mf}}}
  - component: {{fileID: {gate_mr}}}
  - component: {{fileID: {gate_col}}}
  - component: {{fileID: {gate_lt}}}
  - component: {{fileID: {goal_victory_mb}}}
  m_Layer: 0
  m_Name: Victory_Shrine_Gate (Meta Final)
  m_TagString: Finish
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
    sg.add_block(4, gate_tr, f"""
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {gate_go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 11.2, z: 163}}
  m_LocalScale: {{x: 1.5, y: 1.5, z: 1.5}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
""")
    sg.add_block(33, gate_mf, f"""
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {gate_go}}}
  m_Mesh: {{fileID: 4300000, guid: {GUIDS['Model_Portal_VictoryGate']}, type: 3}}
""")
    sg.add_block(23, gate_mr, f"""
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {gate_go}}}
  m_Enabled: 1
  m_CastShadows: 1
  m_ReceiveShadows: 1
  m_Materials:
  - {{fileID: 2100000, guid: {GUIDS['Mat_PortalVortex']}, type: 2}}
""")
    sg.add_block(65, gate_col, f"""
BoxCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {gate_go}}}
  m_Material: {{fileID: 0}}
  m_IsTrigger: 1
  m_Size: {{x: 3.5, y: 5.0, z: 2.5}}
  m_Center: {{x: 0, y: 2.5, z: 0}}
""")
    sg.add_block(108, gate_lt, f"""
Light:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {gate_go}}}
  m_Enabled: 1
  serializedVersion: 11
  m_Type: 2
  m_Color: {{r: 0.75, g: 0.35, b: 1, a: 1}}
  m_Intensity: 4.5
  m_Range: 12
  m_Lightmapping: 4
""")
    sg.add_block(114, goal_victory_mb, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {gate_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUIDS['GoalVictory']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::GoalVictory
  victoryCanvas: {{fileID: {victory_go}}}
  victoryTitleText: {{fileID: {victory_title_tmp}}}
  victoryStarsText: {{fileID: {victory_stars_tmp}}}
  victoryTimeText: {{fileID: {victory_time_tmp}}}
  victoryParticles: {{fileID: 0}}
  victoryAudioSource: {{fileID: 0}}
  goalLight: {{fileID: {gate_lt}}}
""")

    # Write out scene file
    os.makedirs(os.path.dirname(SCENE_PATH), exist_ok=True)
    with open(SCENE_PATH, "w") as f:
        f.write("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n")
        for b in sg.blocks:
            f.write(b)
    print(f"Generated complete Unity scene: {SCENE_PATH} with {len(sg.blocks)} blocks!")

if __name__ == "__main__":
    generate_scene()
