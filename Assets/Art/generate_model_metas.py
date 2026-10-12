import hashlib
import os

models_dir = "Assets/Art/Models"
for fname in os.listdir(models_dir):
    if not fname.endswith(".obj"):
        continue
    name = os.path.splitext(fname)[0]
    meta_path = f"{models_dir}/{fname}.meta"
    if os.path.exists(meta_path):
        continue
    guid = hashlib.md5(f"model_obj_{name}_v1".encode()).hexdigest()
    content = f"""fileFormatVersion: 2
guid: {guid}
ModelImporter:
  serializedVersion: 22200
  internalIDToNameTable:
  - first:
      1: 100000
    second: //RootNode
  - first:
      4: 400000
    second: //RootNode
  - first:
      23: 2300000
    second: //RootNode
  - first:
      33: 3300000
    second: //RootNode
  - first:
      43: 4300000
    second: {name}
  externalObjects: {{}}
  materials:
    materialImportMode: 0
    materialName: 0
    materialSearch: 1
    materialLocation: 0
  animations:
    legacyGenerateAnimations: 4
    bakeSimulation: 0
    resampleCurves: 1
    optimizeGameObjects: 0
    removeConstantScaleCurves: 0
    motionNodeName: 
    rigImportErrors: 
    rigImportWarnings: 
    animationImportErrors: 
    animationImportWarnings: 
    animationRetargetingWarnings: 
    animationDoRetargetingWarnings: 0
    importAnimatedCustomProperties: 0
    importConstraints: 0
    animationCompression: 1
    animationRotationError: 0.5
    animationPositionError: 0.5
    animationScaleError: 0.5
    animationWrapMode: 0
    extraExposedTransformPaths: []
    extraUserProperties: []
    clipAnimations: []
    isReadable: 1
  meshes:
    lODScreenPercentages: []
    globalScale: 1
    useFileUnits: 1
    keepQuads: 0
    weldVertices: 1
    bakeAxisConversion: 0
    importBlendShapes: 1
    importCameras: 0
    importLights: 0
    nodeNameToLower: 0
    generateSecondaryUV: 0
"""
    with open(meta_path, "w") as f:
        f.write(content)
    print(f"Created meta for {fname} (guid: {guid})")
