import hashlib
import os

materials = [
    {
        "name": "M_Island_Grass",
        "color": (0.28, 0.68, 0.32, 1.0),
        "metallic": 0.0,
        "smoothness": 0.15,
        "emission": (0, 0, 0, 1)
    },
    {
        "name": "M_Island_Stone",
        "color": (0.42, 0.45, 0.52, 1.0),
        "metallic": 0.1,
        "smoothness": 0.2,
        "emission": (0, 0, 0, 1)
    },
    {
        "name": "M_Island_Ruins",
        "color": (0.62, 0.58, 0.50, 1.0),
        "metallic": 0.05,
        "smoothness": 0.25,
        "emission": (0, 0, 0, 1)
    },
    {
        "name": "M_Grapple_Rune",
        "color": (0.1, 0.85, 0.95, 1.0),
        "metallic": 0.2,
        "smoothness": 0.8,
        "emission": (0.2, 1.8, 2.2, 1.0)
    },
    {
        "name": "M_Grapple_Gold",
        "color": (0.88, 0.72, 0.25, 1.0),
        "metallic": 0.85,
        "smoothness": 0.65,
        "emission": (0.1, 0.08, 0.02, 1.0)
    },
    {
        "name": "M_Hazard_Spike",
        "color": (0.22, 0.18, 0.20, 1.0),
        "metallic": 0.85,
        "smoothness": 0.7,
        "emission": (0.2, 0.05, 0.05, 1.0)
    },
    {
        "name": "M_Hazard_Saw",
        "color": (0.85, 0.25, 0.15, 1.0),
        "metallic": 0.7,
        "smoothness": 0.6,
        "emission": (1.5, 0.3, 0.1, 1.0)
    },
    {
        "name": "M_Hazard_Laser",
        "color": (1.0, 0.1, 0.2, 1.0),
        "metallic": 0.0,
        "smoothness": 0.9,
        "emission": (3.0, 0.2, 0.4, 1.0)
    },
    {
        "name": "M_Enemy_Hull",
        "color": (0.25, 0.28, 0.38, 1.0),
        "metallic": 0.75,
        "smoothness": 0.6,
        "emission": (0, 0, 0, 1)
    },
    {
        "name": "M_Enemy_Eye_Alert",
        "color": (1.0, 0.15, 0.1, 1.0),
        "metallic": 0.1,
        "smoothness": 0.9,
        "emission": (2.5, 0.2, 0.1, 1.0)
    },
    {
        "name": "M_PowerCell_Core",
        "color": (0.3, 0.9, 1.0, 1.0),
        "metallic": 0.3,
        "smoothness": 0.85,
        "emission": (0.5, 2.0, 2.5, 1.0)
    },
    {
        "name": "M_Portal_Gateway",
        "color": (0.4, 0.2, 0.9, 1.0),
        "metallic": 0.2,
        "smoothness": 0.9,
        "emission": (1.2, 0.6, 2.8, 1.0)
    },
    {
        "name": "M_Star_Gold",
        "color": (1.0, 0.82, 0.1, 1.0),
        "metallic": 0.5,
        "smoothness": 0.8,
        "emission": (2.0, 1.5, 0.2, 1.0)
    },
    {
        "name": "M_Checkpoint_Beacon",
        "color": (0.15, 0.9, 0.45, 1.0),
        "metallic": 0.2,
        "smoothness": 0.85,
        "emission": (0.3, 2.2, 0.8, 1.0)
    },
    {
        "name": "M_Moving_Platform",
        "color": (0.35, 0.50, 0.68, 1.0),
        "metallic": 0.5,
        "smoothness": 0.4,
        "emission": (0.05, 0.1, 0.2, 1.0)
    }
]

out_dir = "Assets/Art/Materials"
os.makedirs(out_dir, exist_ok=True)

for m in materials:
    name = m["name"]
    mat_path = f"{out_dir}/{name}.mat"
    meta_path = f"{mat_path}.meta"
    
    guid = hashlib.md5(f"material_{name}_v1".encode()).hexdigest()
    
    has_emission = any(e > 0.01 for e in m["emission"][:3])
    keywords = "  m_ValidKeywords:\n  - _EMISSION" if has_emission else "  m_ValidKeywords: []"
    
    mat_content = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &-7655408878415146952
MonoBehaviour:
  m_ObjectHideFlags: 11
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: d0353a89b1f911e48b9e16bdc9f2e058, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Unity.RenderPipelines.Universal.Editor::UnityEditor.Rendering.Universal.AssetVersion
  version: 10
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  m_Shader: {{fileID: 4800000, guid: 933532a4fcc9baf4fa0491de14d08ed7, type: 3}}
  m_Parent: {{fileID: 0}}
  m_ModifiedSerializedProperties: 0
{keywords}
  m_InvalidKeywords: []
  m_LightmapFlags: {4 if has_emission else 0}
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap:
    RenderType: Opaque
  disabledShaderPasses:
  - MOTIONVECTORS
  m_LockedProperties: 
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _BaseMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _BumpMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _EmissionMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _MainTex:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _MetallicGlossMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _OcclusionMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _SpecGlossMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    m_Ints: []
    m_Floats:
    - _AlphaClip: 0
    - _Blend: 0
    - _BumpScale: 1
    - _Cull: 2
    - _Cutoff: 0.5
    - _DstBlend: 0
    - _EnvironmentReflections: 1
    - _GlossMapScale: 1
    - _Glossiness: {m['smoothness']}
    - _GlossyReflections: 1
    - _Metallic: {m['metallic']}
    - _OcclusionStrength: 1
    - _QueueOffset: 0
    - _ReceiveShadows: 1
    - _Smoothness: {m['smoothness']}
    - _SpecularHighlights: 1
    - _SrcBlend: 1
    - _Surface: 0
    - _WorkflowMode: 1
    - _ZWrite: 1
    m_Colors:
    - _BaseColor: {{r: {m['color'][0]}, g: {m['color'][1]}, b: {m['color'][2]}, a: {m['color'][3]}}}
    - _Color: {{r: {m['color'][0]}, g: {m['color'][1]}, b: {m['color'][2]}, a: {m['color'][3]}}}
    - _EmissionColor: {{r: {m['emission'][0]}, g: {m['emission'][1]}, b: {m['emission'][2]}, a: {m['emission'][3]}}}
    - _SpecColor: {{r: 0.2, g: 0.2, b: 0.2, a: 1}}
  m_BuildTextureStacks: []
  m_AllowLocking: 1
"""
    with open(mat_path, "w") as f:
        f.write(mat_content)
        
    meta_content = f"""fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 2100000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
    with open(meta_path, "w") as f:
        f.write(meta_content)
        
    print(f"Created material {name} (guid: {guid})")
