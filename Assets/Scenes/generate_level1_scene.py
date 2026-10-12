import re
import hashlib
import os

with open('Assets/Scenes/Testing/Prototype.unity') as f:
    text = f.read()

docs = text.split('--- !u!')

# Set of preserved GameObjects and PrefabInstances
preserved_gos = {
    '564171408', # Directional Light
    '845054624', # GM
    '1471267607', # Canvas
    '332203870', # StarScore
    '343517891', # LeftStick
    '782989298', # LookStick
    '203305028', # ToolButtton
    '1135571682', # ChangeTool_Button
    '932267917', # Jump_Button
    '1294483370', # Crouch_Button
    '1430419317', # EventSystem
    '560267824', # Player PrefabInstance
    '560267825', # Player GameObject (stripped)
    '560267826', # Player Camera GameObject (stripped)
    '1048108835', # Tool
}

# Collect all component and GameObject IDs that belong to the preserved entities
# First pass: map components of preserved GOs
preserved_all_ids = set(preserved_gos)

for doc in docs[1:]:
    lines = doc.split('\n')
    header = lines[0].strip()
    id_match = re.match(r'(\d+)\s+&(\d+)', header)
    if not id_match:
        continue
    type_id, doc_id = id_match.groups()
    
    # Check if doc belongs to a preserved GO
    for line in lines:
        if 'm_GameObject: {fileID: ' in line:
            m = re.search(r'm_GameObject: \{fileID: (\d+)\}', line)
            if m and m.group(1) in preserved_gos:
                preserved_all_ids.add(doc_id)
        if 'm_PrefabInstance: {fileID: ' in line:
            m = re.search(r'm_PrefabInstance: \{fileID: (\d+)\}', line)
            if m and m.group(1) in preserved_gos:
                preserved_all_ids.add(doc_id)

print(f"Preserved {len(preserved_all_ids)} elements from prototype base")

base_docs = []
# Always keep scene root settings (docs 1 to 4: Occlusion, RenderSettings, LightmapSettings, NavMeshSettings)
for doc in docs[1:5]:
    base_docs.append(doc)

for doc in docs[5:]:
    lines = doc.split('\n')
    header = lines[0].strip()
    id_match = re.match(r'(\d+)\s+&(\d+)', header)
    if id_match:
        doc_id = id_match.group(2)
        if doc_id in preserved_all_ids:
            # Let's adjust Player initial position to (0, 2.5, -4)
            if doc_id == '560267824': # PrefabInstance
                # Adjust modifications
                doc = re.sub(
                    r'(propertyPath: m_LocalPosition\.y\s+value: )[^\n]+',
                    r'\g<1>2.5',
                    doc
                )
                doc = re.sub(
                    r'(propertyPath: m_LocalPosition\.z\s+value: )[^\n]+',
                    r'\g<1>-4',
                    doc
                )
                doc = re.sub(
                    r'(propertyPath: m_LocalPosition\.x\s+value: )[^\n]+',
                    r'\g<1>0',
                    doc
                )
            base_docs.append(doc)

print(f"Base scene documents retained: {len(base_docs)}")

# Guids for models
BEACON_MESH = "55622c49b3e233ffe062929eb171df8d"
SPIKE_MESH = "6159d7b80438d52e70b8c36fa36629cf"
SAW_MESH = "abc8aca8ac56d5c7ed4faa1970c31aa9"
DRONE_MESH = "a4f0eecdd804a66e20452d420daa832f"
CELL_MESH = "d21ba642e73ace5ca89701a12887d7fe"
STAR_MESH = "98631669855bc416e20e26b2ff3ea65e"
OBELISK_MESH = "e445573eef3b5afd8873f96b79a7c23f"
PORTAL_MESH = "059eff564138660a03a7aed43873b6d7"
HEX_MESH = "6cae4f1aa22ff4a33e3e449cf5b5b6a8"
PLATEAU_MESH = "335f2f9e535f9aa68767d47b8ebfda77"
ARENA_MESH = "49e5e71d3a875f20cfeb5ac0bd408043"

# Guids for materials
MAT_GRASS = "7877eb5468e6afdbfb7cdd6fffc9c782"
MAT_STONE = "045ef3a0b1862eec6e9a9b78c5ab6d81"
MAT_RUNE = "713f6c06713d0bfe5635c82c196d2c3c"
MAT_SPIKE = "7c0b77b4cddf02c8256c9066aafa2115"
MAT_SAW = "6bd31f81f8a6d7a149ced12404452602"
MAT_DRONE = "7706fbe7e0120e4640b5ca7d794bc997"
MAT_CELL = "a6e2cd147c1219df5265ada2d9640e12"
MAT_STAR = "62490d9fd92b885e04accc4c7e12dfdc"
MAT_OBELISK = "7576e088d522a23dd90fc92b98d71781"
MAT_PORTAL = "a703c281ec87ae0b27f7d1a88587cd4c"
MAT_MOVING = "f36ada40050bb2b05710ce105ca60148"

# Guids for scripts
ENEMY_AI_GUID = "d9390ae756fb44aebe347009293b4326"
FLOATING_BOBBER_GUID = "b5d68ea165cb438c8197886904fd95df"
GOAL_CONTROLLER_GUID = "c01ed7bf32f54e2ab6286f26c1711d09"
HAZARD_GUID = "25e5602c3353464aa39d0b91fb85c272"
MOVING_OBSTACLE_GUID = "a4a32998ed8f4594a607b864f142fa2e"
POOLABLE_CONTROLLER_GUID = "0e3f975a7f4bc41d6b168cb05932b95f"
RESPAWN_CONTROLLER_GUID = "8a8ddc71bd8a245a697ea47d31187afb"
ROTATOR_GUID = "5c809e5ff2dd40609264082d8cd1e0de"
STAR_CONTROLLER_GUID = "0a95c6a505dbd41989ddfb26721a0131"

next_id = 900000000
def get_id():
    global next_id
    next_id += 1
    return next_id

level_docs = []

def add_island(name, pos, scale, mesh_guid, mat_guid):
    go_id = get_id()
    tr_id = get_id()
    mf_id = get_id()
    mr_id = get_id()
    mc_id = get_id()
    
    doc = f"""1 &{go_id}
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
  - component: {{fileID: {mc_id}}}
  m_Layer: 0
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 2147483647
  m_IsActive: 1
--- !u!4 &{tr_id}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: {pos[0]}, y: {pos[1]}, z: {pos[2]}}}
  m_LocalScale: {{x: {scale[0]}, y: {scale[1]}, z: {scale[2]}}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!33 &{mf_id}
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Mesh: {{fileID: 4300000, guid: {mesh_guid}, type: 3}}
--- !u!23 &{mr_id}
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
--- !u!64 &{mc_id}
MeshCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Material: {{fileID: 0}}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_LayerOverridePriority: 0
  m_IsTrigger: 0
  m_ProvidesContacts: 0
  m_Enabled: 1
  serializedVersion: 5
  m_Convex: 0
  m_CookingOptions: 30
  m_Mesh: {{fileID: 4300000, guid: {mesh_guid}, type: 3}}"""
    level_docs.append(doc)

def add_grapple_beacon(name, pos):
    go_id = get_id()
    tr_id = get_id()
    mf_id = get_id()
    mr_id = get_id()
    sc_id = get_id()
    rot_id = get_id()
    bob_id = get_id()
    
    doc = f"""1 &{go_id}
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
  - component: {{fileID: {sc_id}}}
  - component: {{fileID: {rot_id}}}
  - component: {{fileID: {bob_id}}}
  m_Layer: 8
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{tr_id}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: {pos[0]}, y: {pos[1]}, z: {pos[2]}}}
  m_LocalScale: {{x: 1.3, y: 1.3, z: 1.3}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!33 &{mf_id}
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Mesh: {{fileID: 4300000, guid: {BEACON_MESH}, type: 3}}
--- !u!23 &{mr_id}
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
  - {{fileID: 2100000, guid: {MAT_RUNE}, type: 2}}
--- !u!135 &{sc_id}
SphereCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Material: {{fileID: 0}}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_LayerOverridePriority: 0
  m_IsTrigger: 0
  m_ProvidesContacts: 0
  m_Enabled: 1
  serializedVersion: 3
  m_Radius: 2.2
  m_Center: {{x: 0, y: 0, z: 0}}
--- !u!114 &{rot_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {ROTATOR_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::Rotator
  rotationAngles: {{x: 0, y: 40, z: 15}}
  space: 0
--- !u!114 &{bob_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {FLOATING_BOBBER_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::FloatingBobber
  bobAmplitude: 0.35
  bobFrequency: 1.8
  timeOffset: 0"""
    level_docs.append(doc)

def add_hazard_spikes(name, pos, scale=(1, 1, 1)):
    go_id = get_id()
    tr_id = get_id()
    mf_id = get_id()
    mr_id = get_id()
    bc_id = get_id()
    haz_id = get_id()
    
    doc = f"""1 &{go_id}
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
  - component: {{fileID: {bc_id}}}
  - component: {{fileID: {haz_id}}}
  m_Layer: 0
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{tr_id}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: {pos[0]}, y: {pos[1]}, z: {pos[2]}}}
  m_LocalScale: {{x: {scale[0]}, y: {scale[1]}, z: {scale[2]}}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!33 &{mf_id}
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Mesh: {{fileID: 4300000, guid: {SPIKE_MESH}, type: 3}}
--- !u!23 &{mr_id}
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
  - {{fileID: 2100000, guid: {MAT_SPIKE}, type: 2}}
--- !u!65 &{bc_id}
BoxCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Material: {{fileID: 0}}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_LayerOverridePriority: 0
  m_IsTrigger: 1
  m_ProvidesContacts: 0
  m_Enabled: 1
  serializedVersion: 3
  m_Size: {{x: 2.4, y: 1.2, z: 2.4}}
  m_Center: {{x: 0, y: 0.6, z: 0}}
--- !u!114 &{haz_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {HAZARD_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::Hazard
  instantKill: 0
  damageAmount: 1
  damageCooldown: 1.0
  knockbackForce: 6.0"""
    level_docs.append(doc)

def add_moving_platform(name, pos, move_offset, speed=3.0):
    go_id = get_id()
    tr_id = get_id()
    mf_id = get_id()
    mr_id = get_id()
    mc_id = get_id()
    bc_trig_id = get_id()
    mov_id = get_id()
    
    doc = f"""1 &{go_id}
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
  - component: {{fileID: {mc_id}}}
  - component: {{fileID: {bc_trig_id}}}
  - component: {{fileID: {mov_id}}}
  m_Layer: 0
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{tr_id}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: {pos[0]}, y: {pos[1]}, z: {pos[2]}}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!33 &{mf_id}
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Mesh: {{fileID: 4300000, guid: {HEX_MESH}, type: 3}}
--- !u!23 &{mr_id}
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
  - {{fileID: 2100000, guid: {MAT_MOVING}, type: 2}}
--- !u!64 &{mc_id}
MeshCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Material: {{fileID: 0}}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_LayerOverridePriority: 0
  m_IsTrigger: 0
  m_ProvidesContacts: 0
  m_Enabled: 1
  serializedVersion: 5
  m_Convex: 0
  m_CookingOptions: 30
  m_Mesh: {{fileID: 4300000, guid: {HEX_MESH}, type: 3}}
--- !u!65 &{bc_trig_id}
BoxCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Material: {{fileID: 0}}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_LayerOverridePriority: 0
  m_IsTrigger: 1
  m_ProvidesContacts: 0
  m_Enabled: 1
  serializedVersion: 3
  m_Size: {{x: 4.0, y: 1.0, z: 4.0}}
  m_Center: {{x: 0, y: 0.6, z: 0}}
--- !u!114 &{mov_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {MOVING_OBSTACLE_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::MovingObstacle
  movementType: 0
  startOffset: {{x: 0, y: 0, z: 0}}
  moveOffset: {{x: {move_offset[0]}, y: {move_offset[1]}, z: {move_offset[2]}}}
  moveSpeed: {speed}
  pauseDuration: 0.8
  rotationSpeed: {{x: 0, y: 0, z: 0}}"""
    level_docs.append(doc)

def add_spinning_saw(name, pos):
    go_id = get_id()
    tr_id = get_id()
    mf_id = get_id()
    mr_id = get_id()
    sc_id = get_id()
    mov_id = get_id()
    haz_id = get_id()
    
    doc = f"""1 &{go_id}
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
  - component: {{fileID: {sc_id}}}
  - component: {{fileID: {mov_id}}}
  - component: {{fileID: {haz_id}}}
  m_Layer: 0
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{tr_id}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: {pos[0]}, y: {pos[1]}, z: {pos[2]}}}
  m_LocalScale: {{x: 1.3, y: 1.3, z: 1.3}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!33 &{mf_id}
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Mesh: {{fileID: 4300000, guid: {SAW_MESH}, type: 3}}
--- !u!23 &{mr_id}
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
  - {{fileID: 2100000, guid: {MAT_SAW}, type: 2}}
--- !u!135 &{sc_id}
SphereCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Material: {{fileID: 0}}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_LayerOverridePriority: 0
  m_IsTrigger: 1
  m_ProvidesContacts: 0
  m_Enabled: 1
  serializedVersion: 3
  m_Radius: 1.8
  m_Center: {{x: 0, y: 0, z: 0}}
--- !u!114 &{mov_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {MOVING_OBSTACLE_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::MovingObstacle
  movementType: 1
  rotationSpeed: {{x: 0, y: 360, z: 0}}
--- !u!114 &{haz_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {HAZARD_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::Hazard
  instantKill: 0
  damageAmount: 1
  damageCooldown: 0.8
  knockbackForce: 8.0"""
    level_docs.append(doc)

def add_enemy_drone(name, pos):
    go_id = get_id()
    tr_id = get_id()
    mf_id = get_id()
    mr_id = get_id()
    sc_id = get_id()
    rb_id = get_id()
    ai_id = get_id()
    
    doc = f"""1 &{go_id}
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
  - component: {{fileID: {sc_id}}}
  - component: {{fileID: {rb_id}}}
  - component: {{fileID: {ai_id}}}
  m_Layer: 0
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{tr_id}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: {pos[0]}, y: {pos[1]}, z: {pos[2]}}}
  m_LocalScale: {{x: 1.1, y: 1.1, z: 1.1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!33 &{mf_id}
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Mesh: {{fileID: 4300000, guid: {DRONE_MESH}, type: 3}}
--- !u!23 &{mr_id}
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
  - {{fileID: 2100000, guid: {MAT_DRONE}, type: 2}}
--- !u!135 &{sc_id}
SphereCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Material: {{fileID: 0}}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_LayerOverridePriority: 0
  m_IsTrigger: 0
  m_ProvidesContacts: 0
  m_Enabled: 1
  serializedVersion: 3
  m_Radius: 1.0
  m_Center: {{x: 0, y: 0, z: 0}}
--- !u!54 &{rb_id}
Rigidbody:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  serializedVersion: 4
  m_Mass: 2
  m_Drag: 1
  m_AngularDrag: 0.5
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
  m_UseGravity: 0
  m_IsKinematic: 1
  m_Interpolate: 0
  m_Constraints: 0
  m_CollisionDetection: 0
--- !u!114 &{ai_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {ENEMY_AI_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::EnemyAI
  detectionRange: 18
  attackRange: 2.2
  chaseSpeed: 4.2
  turnSpeed: 6
  hoverHeight: 1.4
  attackDamage: 1
  attackCooldown: 1.8
  maxHealth: 2
  eyeRenderer: {{fileID: {mr_id}}}
  idleEyeColor: {{r: 0.2, g: 0.8, b: 1, a: 1}}
  alertEyeColor: {{r: 1, g: 0.1, b: 0.1, a: 1}}"""
    level_docs.append(doc)

def add_power_cell(name, pos):
    go_id = get_id()
    tr_id = get_id()
    mf_id = get_id()
    mr_id = get_id()
    bc_id = get_id()
    rb_id = get_id()
    pool_id = get_id()
    
    doc = f"""1 &{go_id}
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
  - component: {{fileID: {bc_id}}}
  - component: {{fileID: {rb_id}}}
  - component: {{fileID: {pool_id}}}
  m_Layer: 7
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{tr_id}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: {pos[0]}, y: {pos[1]}, z: {pos[2]}}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!33 &{mf_id}
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Mesh: {{fileID: 4300000, guid: {CELL_MESH}, type: 3}}
--- !u!23 &{mr_id}
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
  - {{fileID: 2100000, guid: {MAT_CELL}, type: 2}}
--- !u!65 &{bc_id}
BoxCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Material: {{fileID: 0}}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_LayerOverridePriority: 0
  m_IsTrigger: 0
  m_ProvidesContacts: 0
  m_Enabled: 1
  serializedVersion: 3
  m_Size: {{x: 0.9, y: 0.9, z: 0.9}}
  m_Center: {{x: 0, y: 0, z: 0}}
--- !u!54 &{rb_id}
Rigidbody:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  serializedVersion: 4
  m_Mass: 1.5
  m_Drag: 0.2
  m_AngularDrag: 0.5
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
  m_CollisionDetection: 2
--- !u!114 &{pool_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {POOLABLE_CONTROLLER_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::PoolableController
  absorptionDuration: 0.8
  spiralRadius: 0.6
  spiralTurns: 2.5
  scaleRestoreDuration: 0.25
  absorptionDistance: 0.35"""
    level_docs.append(doc)

def add_star(name, pos):
    go_id = get_id()
    tr_id = get_id()
    mf_id = get_id()
    mr_id = get_id()
    sc_id = get_id()
    star_id = get_id()
    rot_id = get_id()
    bob_id = get_id()
    
    doc = f"""1 &{go_id}
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
  - component: {{fileID: {sc_id}}}
  - component: {{fileID: {star_id}}}
  - component: {{fileID: {rot_id}}}
  - component: {{fileID: {bob_id}}}
  m_Layer: 0
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{tr_id}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: {pos[0]}, y: {pos[1]}, z: {pos[2]}}}
  m_LocalScale: {{x: 1.1, y: 1.1, z: 1.1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!33 &{mf_id}
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Mesh: {{fileID: 4300000, guid: {STAR_MESH}, type: 3}}
--- !u!23 &{mr_id}
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
  - {{fileID: 2100000, guid: {MAT_STAR}, type: 2}}
--- !u!135 &{sc_id}
SphereCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Material: {{fileID: 0}}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_LayerOverridePriority: 0
  m_IsTrigger: 1
  m_ProvidesContacts: 0
  m_Enabled: 1
  serializedVersion: 3
  m_Radius: 1.2
  m_Center: {{x: 0, y: 0, z: 0}}
--- !u!114 &{star_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {STAR_CONTROLLER_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::StarController
--- !u!114 &{rot_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {ROTATOR_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::Rotator
  rotationAngles: {{x: 0, y: 70, z: 0}}
  space: 0
--- !u!114 &{bob_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {FLOATING_BOBBER_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::FloatingBobber
  bobAmplitude: 0.25
  bobFrequency: 2.2
  timeOffset: 0"""
    level_docs.append(doc)

def add_checkpoint(name, pos):
    go_id = get_id()
    tr_id = get_id()
    mf_id = get_id()
    mr_id = get_id()
    bc_id = get_id()
    resp_id = get_id()
    
    doc = f"""1 &{go_id}
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
  - component: {{fileID: {bc_id}}}
  - component: {{fileID: {resp_id}}}
  m_Layer: 0
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{tr_id}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: {pos[0]}, y: {pos[1]}, z: {pos[2]}}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!33 &{mf_id}
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Mesh: {{fileID: 4300000, guid: {OBELISK_MESH}, type: 3}}
--- !u!23 &{mr_id}
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
  - {{fileID: 2100000, guid: {MAT_OBELISK}, type: 2}}
--- !u!65 &{bc_id}
BoxCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Material: {{fileID: 0}}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_LayerOverridePriority: 0
  m_IsTrigger: 1
  m_ProvidesContacts: 0
  m_Enabled: 1
  serializedVersion: 3
  m_Size: {{x: 3.5, y: 4.5, z: 3.5}}
  m_Center: {{x: 0, y: 1.5, z: 0}}
--- !u!114 &{resp_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {RESPAWN_CONTROLLER_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::RespawnController"""
    level_docs.append(doc)

def add_victory_goal(name, pos):
    go_id = get_id()
    tr_id = get_id()
    mf_id = get_id()
    mr_id = get_id()
    bc_id = get_id()
    goal_id = get_id()
    
    doc = f"""1 &{go_id}
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
  - component: {{fileID: {bc_id}}}
  - component: {{fileID: {goal_id}}}
  m_Layer: 0
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{tr_id}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: {pos[0]}, y: {pos[1]}, z: {pos[2]}}}
  m_LocalScale: {{x: 1.3, y: 1.3, z: 1.3}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!33 &{mf_id}
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Mesh: {{fileID: 4300000, guid: {PORTAL_MESH}, type: 3}}
--- !u!23 &{mr_id}
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
  - {{fileID: 2100000, guid: {MAT_PORTAL}, type: 2}}
--- !u!65 &{bc_id}
BoxCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Material: {{fileID: 0}}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_LayerOverridePriority: 0
  m_IsTrigger: 1
  m_ProvidesContacts: 0
  m_Enabled: 1
  serializedVersion: 3
  m_Size: {{x: 4.5, y: 5.5, z: 3.0}}
  m_Center: {{x: 0, y: 2.7, z: 0}}
--- !u!114 &{goal_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GOAL_CONTROLLER_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::GoalController
  rotationSpeedOnWin: 120"""
    level_docs.append(doc)

def add_void_killzone():
    go_id = get_id()
    tr_id = get_id()
    bc_id = get_id()
    haz_id = get_id()
    
    doc = f"""1 &{go_id}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {tr_id}}}
  - component: {{fileID: {bc_id}}}
  - component: {{fileID: {haz_id}}}
  m_Layer: 0
  m_Name: VoidKillZone
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{tr_id}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: -18, z: 100}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!65 &{bc_id}
BoxCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Material: {{fileID: 0}}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_LayerOverridePriority: 0
  m_IsTrigger: 1
  m_ProvidesContacts: 0
  m_Enabled: 1
  serializedVersion: 3
  m_Size: {{x: 300, y: 10, z: 400}}
  m_Center: {{x: 0, y: 0, z: 0}}
--- !u!114 &{haz_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {HAZARD_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::Hazard
  instantKill: 1
  damageAmount: 99
  damageCooldown: 0.1
  knockbackForce: 0"""
    level_docs.append(doc)

# 1. ZONE 1: START ISLAND (Plateau at 0, 0, 0)
add_island("Start_Island", (0, 0, 0), (1.1, 1.0, 1.1), PLATEAU_MESH, MAT_GRASS)
add_checkpoint("Checkpoint_1_Start", (0, 1.0, -1.0))
# Stepping stones leading forward
add_island("Step_Hex_1", (0, 0.5, 14), (0.9, 0.9, 0.9), HEX_MESH, MAT_STONE)
add_island("Step_Hex_2", (3.0, 1.5, 22), (0.8, 0.9, 0.8), HEX_MESH, MAT_STONE)
# Star 1 on overlook perch
add_island("Star1_Perch", (6.5, 3.5, 26), (0.6, 0.8, 0.6), HEX_MESH, MAT_STONE)
add_star("SecretStar_1", (6.5, 5.0, 26))

# 2. ZONE 2: GRAPPLE CHASM (Z: 25 to 75)
# 3 Floating Grapple Beacons on Layer 8
add_grapple_beacon("GrappleBeacon_Alpha", (1.0, 7.5, 37.0))
add_grapple_beacon("GrappleBeacon_Beta", (-3.5, 10.0, 53.0))
add_grapple_beacon("GrappleBeacon_Gamma", (2.0, 11.5, 69.0))

# 3. ZONE 3: GAUNTLET ISLAND (Z: 85)
add_island("Mid_Island_Gauntlet", (0, 8.0, 85.0), (1.0, 1.0, 1.2), PLATEAU_MESH, MAT_GRASS)
add_checkpoint("Checkpoint_2_Mid", (0, 9.2, 85.0))
# Spike obstacles blocking direct lanes
add_hazard_spikes("HazardSpikes_LaneLeft", (-2.2, 8.7, 94.0))
add_hazard_spikes("HazardSpikes_LaneRight", (2.2, 8.7, 98.0))

# Moving Hex Platform across the gap
add_moving_platform("MovingHexPlatform_Gap", (0, 8.5, 107.0), (0, 2.0, 16.0), speed=3.2)
# Rotating buzzsaw obstacle between platforms
add_spinning_saw("HazardSaw_MiddleChasm", (0, 10.6, 115.0))
# Star 2 on side anchor perch
add_grapple_beacon("GrappleBeacon_Star2_Anchor", (-7.0, 14.0, 115.0))
add_star("SecretStar_2", (-7.0, 11.5, 115.0))

# 4. ZONE 4: SENTINEL ARENA (Z: 142)
add_island("Combat_Arena_Island", (0, 10.0, 142.0), (1.1, 1.0, 1.1), ARENA_MESH, MAT_STONE)
# 2 Sentinel Enemies (Basic enemies that chase and attack)
add_enemy_drone("Sentinel_Drone_Alpha", (-4.0, 12.0, 141.0))
add_enemy_drone("Sentinel_Drone_Beta", (4.0, 12.0, 145.0))
# 4 Vacuum Power Cells (Layer 7 Poolable with Rigidbody to shoot enemies)
add_power_cell("PowerCell_NorthWest", (-3.2, 10.8, 137.0))
add_power_cell("PowerCell_NorthEast", (3.2, 10.8, 137.0))
add_power_cell("PowerCell_SouthWest", (-3.2, 10.8, 147.0))
add_power_cell("PowerCell_SouthEast", (3.2, 10.8, 147.0))
add_checkpoint("Checkpoint_3_PostArena", (0, 11.2, 154.0))

# 5. ZONE 5: SUMMIT & VICTORY PORTAL
add_island("Summit_Ascent_Hex1", (4.5, 13.5, 164.0), (0.85, 0.9, 0.85), HEX_MESH, MAT_STONE)
add_island("Summit_Ascent_Hex2", (-3.0, 16.0, 174.0), (0.85, 0.9, 0.85), HEX_MESH, MAT_STONE)
add_grapple_beacon("GrappleBeacon_Summit", (-3.0, 20.0, 174.0))
# Star 3 on summit overlook
add_star("SecretStar_3", (-7.0, 18.5, 174.0))

# Grand Summit Plateau
add_island("Summit_Plateau", (0, 18.0, 194.0), (1.2, 1.1, 1.2), PLATEAU_MESH, MAT_GRASS)
# The Grand Victory Portal (Goal)
add_victory_goal("Grand_Victory_Portal", (0, 19.0, 200.0))

# 6. VOID KILL ZONE
add_void_killzone()

print(f"Generated {len(level_docs)} Level 1 custom elements")

# Combine everything
full_scene_text = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!" + "\n--- !u!".join(base_docs + level_docs) + "\n"

os.makedirs('Assets/Scenes/Level1', exist_ok=True)
scene_path = 'Assets/Scenes/Level1/Level1.unity'
with open(scene_path, 'w') as f:
    f.write(full_scene_text)

meta_guid = hashlib.md5("scene_Level1_v1".encode()).hexdigest()
meta_content = f"""fileFormatVersion: 2
guid: {meta_guid}
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
with open(f'{scene_path}.meta', 'w') as f:
    f.write(meta_content)

print(f"Successfully generated {scene_path} (guid: {meta_guid})")
