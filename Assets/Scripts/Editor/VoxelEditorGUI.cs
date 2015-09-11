﻿using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;


[CustomEditor(typeof(Vox.VoxelEditor))]
public class VoxelEditorGUI : Editor {

	protected const string numForm = "##,0.000";
	protected const string numFormInt = "##,#";
	protected static readonly GUIContent[] modes = {new GUIContent("Manage"), new GUIContent("Sculpt"), new GUIContent("Mask")};
	protected static readonly GUIContent[] brushes = {new GUIContent("Sphere"), new GUIContent("Rectangle"), new GUIContent("Smooth")};
	protected static readonly GUIContent[] generationModes = {new GUIContent("Flat"), new GUIContent("Sphere"), new GUIContent("Procedural"), new GUIContent("Heightmaps")};

	private SerializedObject ob;
	private GUIStyle labelBigFont = null;
	private GUIStyle foldoutBigFont = null;
	private GUIStyle buttonBigFont = null;
	private GUIStyle tabsBigFont = null;

	// generation parameters
	private bool setupGeneration;
	private int selectedGenerationMode;
	private bool showSubstances;

	private bool showMasks;
	private bool showStatistics;
    private VoxelEditorParameters generationParameters;

    [MenuItem("GameObject/3D Object/Voxel Object")]
	public static void createVoxelObject() {
		Vox.VoxelEditor.createEmpty();
	}

	public void OnEnable() {
		ob = new SerializedObject(target);
		setupGeneration = false;
		showSubstances = false;
		showMasks = true;
		showStatistics = false;
	}

	public override void OnInspectorGUI() {
		labelBigFont = new GUIStyle(GUI.skin.label);
		labelBigFont.margin = new RectOffset(labelBigFont.margin.left, labelBigFont.margin.right, labelBigFont.margin.top +10, labelBigFont.margin.bottom);
		labelBigFont.fontSize = 16;
		foldoutBigFont = new GUIStyle(EditorStyles.foldout);
		foldoutBigFont.margin = new RectOffset(foldoutBigFont.margin.left, foldoutBigFont.margin.right, foldoutBigFont.margin.top +10, foldoutBigFont.margin.bottom);
		foldoutBigFont.fontSize = 16;
		foldoutBigFont.alignment = TextAnchor.LowerLeft;
		buttonBigFont = new GUIStyle(GUI.skin.button);
		buttonBigFont.fontSize = 14;
		tabsBigFont = new GUIStyle(GUI.skin.button);
		tabsBigFont.fixedHeight = 30;

		Vox.VoxelEditor editor = (Vox.VoxelEditor)target;
		ob.UpdateIfDirtyOrScript();

		if (editor.generating()) {
			GUILayout.Label("Generating...", labelBigFont);
			return;
		}

		if (setupGeneration) {
			doGenerationGUI(editor);
			return;
		} else {
			if (!editor.hasVoxelData())
				GUI.enabled = false;
			editor.selectedMode = GUILayout.Toolbar(editor.selectedMode, modes, tabsBigFont, GUILayout.Height(30));
			GUI.enabled = true;

			switch (editor.selectedMode) {
			case 0:
				doManageGUI(editor);
				break;
			case 1:
				doSculptGUI(editor);
				break;
			case 2:
				doMaskGUI(editor);
				break;
			}
		}

		// finally, apply the changes
		ob.ApplyModifiedProperties();
	}

	public void OnSceneGUI() {
		Vox.VoxelEditor editor = (Vox.VoxelEditor)target;
		editor.Update();
		if (editor.selectedMode != 1)
			return;
		int controlId = GUIUtility.GetControlID(FocusType.Passive);
		switch(UnityEngine.Event.current.GetTypeForControl(controlId)) {
		case EventType.MouseDown:
			if (UnityEngine.Event.current.button == 0) {
				GUIUtility.hotControl = controlId;
				applyBrush(editor, HandleUtility.GUIPointToWorldRay(UnityEngine.Event.current.mousePosition));
				UnityEngine.Event.current.Use();
			}
			break;

		case EventType.MouseUp:
			if (UnityEngine.Event.current.button == 0) {
				GUIUtility.hotControl = 0;
				UnityEngine.Event.current.Use();
			}
			break;
		case EventType.MouseMove:
			SceneView.RepaintAll();
			break;
		}
	}

	protected void doMaskGUI(Vox.VoxelEditor editor) {
		editor.maskDisplayAlpha = doSliderFloatField("Mask Display Transparency", editor.maskDisplayAlpha, 0, 1);

		// mask list
		showMasks = doBigFoldout(showMasks, "Masks");
		if (showMasks) {
			SerializedProperty voxelMasks = ob.FindProperty("masks");
			// EditorGUILayout.PropertyField(voxelMasks, new GUIContent("Sculpting Masks"), true);
			InspectorList.doArrayGUISimple(ref voxelMasks);
		}
	}

	protected void doSculptGUI(Vox.VoxelEditor editor) {
		// brush ghost
		editor.ghostBrushAlpha = doSliderFloatField("Brush Ghost Opacity", editor.ghostBrushAlpha, 0, 1);
		
		editor.gridEnabled = EditorGUILayout.Toggle("Snap to Grid", editor.gridEnabled);
        if (editor.gridEnabled) {
            ++EditorGUI.indentLevel;
            editor.gridUseVoxelUnits = EditorGUILayout.Toggle("Use Voxel Units", editor.gridUseVoxelUnits);
			if (editor.gridUseVoxelUnits) {
				float voxelSize = editor.baseSize / (1 << editor.maxDetail);
                editor.gridSize = EditorGUILayout.FloatField("Grid Spacing (Voxels)", editor.gridSize /voxelSize) *voxelSize;
			} else {
                editor.gridSize = EditorGUILayout.FloatField("Grid Spacing (Meters)", editor.gridSize);
            }
			--EditorGUI.indentLevel;
        }

        // brush list
		GUILayout.Label("Brush", labelBigFont);
        editor.selectedBrush = GUILayout.Toolbar(editor.selectedBrush, brushes, GUILayout.MinHeight(20));

		// brush substance type
		string[] substances = new string[editor.voxelSubstances.Length];
		for(int i=0; i<substances.Length; ++i)
			substances[i] = editor.voxelSubstances[i].name;

		// brush size
		switch(editor.selectedBrush) {
		case 0:
            GUILayout.Label("Hold 'Shift' to subtract.");
			editor.sphereBrushSize = doSliderFloatField("Sphere Radius (m)", editor.sphereBrushSize, 0, 100);
			editor.sphereSubstanceOnly = GUILayout.Toggle(editor.sphereSubstanceOnly, "Change Substance Only");
			GUILayout.Label("Substance", labelBigFont);
			editor.sphereBrushSubstance = (byte)GUILayout.SelectionGrid(editor.sphereBrushSubstance, substances, 1);
			break;

		case 1:
			GUILayout.Label("Hold 'Shift' to subtract.");
			GUILayout.BeginHorizontal();
			GUILayout.Label("Dimensions (m)");
			editor.cubeBrushDimensions.x = EditorGUILayout.FloatField(editor.cubeBrushDimensions.x);
			editor.cubeBrushDimensions.y = EditorGUILayout.FloatField(editor.cubeBrushDimensions.y);
			editor.cubeBrushDimensions.z = EditorGUILayout.FloatField(editor.cubeBrushDimensions.z);
//			SerializedProperty cubeBrushDimensions = ob.FindProperty("cubeBrushDimensions");
//			EditorGUILayout.PropertyField(cubeBrushDimensions, new GUIContent("Rectangle Brush Dimensions"), true);
			GUILayout.EndHorizontal();
			
			editor.cubeSubstanceOnly = GUILayout.Toggle(editor.cubeSubstanceOnly, "Change Substance Only");
			GUILayout.Label("Substance", labelBigFont);
			editor.cubeBrushSubstance = (byte)GUILayout.SelectionGrid(editor.cubeBrushSubstance, substances, 1);
			break;

		case 2:
			editor.smoothBrushSize = doSliderFloatField("Radius (m)", editor.smoothBrushSize, 0, 100);
			editor.smoothBrushStrength = doSliderFloatField("Strength", editor.smoothBrushStrength, 0, 5);
			editor.smoothBrushBlurRadius = EditorGUILayout.IntField("Blur Radius", editor.smoothBrushBlurRadius);
			break;
		}

	}

	protected void doManageGUI(Vox.VoxelEditor editor) {

		// actions
		GUILayout.Label ("Actions", labelBigFont);
		if (GUILayout.Button("Generate New")) {
			setupGeneration = true;
			generationParameters = new VoxelEditorParameters();
            generationParameters.setFrom(editor);
        }
		if (editor.hasVoxelData()) {
			if (GUILayout.Button("Clear")) {
				if (EditorUtility.DisplayDialog("Clear Voxels?", "Are you sure you want to clear all voxel data?", "Clear", "Cancel")) {
					editor.wipe();
				}
			}
			if (GUILayout.Button("Reskin", buttonBigFont) && validateSubstances(editor)) {
				if (EditorUtility.DisplayDialog("Regenerate Voxel Meshes?", "Are you sure you want to regenerate all voxel meshes?", "Reskin", "Cancel")) {
					editor.generateRenderers();
				}
			}
			if (GUILayout.Button("Export")) {
				editor.export(EditorUtility.SaveFilePanel("Choose File to Export To", "", "Voxels", "vox"));
			}
		}
		if (GUILayout.Button("Import")) {
			if (!editor.import(EditorUtility.OpenFilePanel("Choose File to Import From", "", "vox"))) {
				EditorUtility.DisplayDialog("Wrong Voxel Format", "The file you chose was an unknown or incompatible voxel format version.", "OK");
			}
		}

		if (!editor.hasVoxelData())
			return;

		GUILayout.Label("Properties", labelBigFont);

		doGeneralPropertiesGUI(editor);

		// TODO: implement LOD and uncomment this
//		// LOD
//		SerializedProperty useLod = ob.FindProperty("useLod");
//		EditorGUILayout.PropertyField(useLod, new GUIContent("Use Level of Detail"));
//		if (useLod.boolValue) {
//			++EditorGUI.indentLevel;
//			SerializedProperty lodDetail = ob.FindProperty("lodDetail");
//			EditorGUILayout.PropertyField(lodDetail, new GUIContent("Target Level of Detail"));
//			if (lodDetail.floatValue > 1000)
//				lodDetail.floatValue = 1000;
//			else if (lodDetail.floatValue < 0.1f)
//				lodDetail.floatValue = 0.1f;
//
//			SerializedProperty curLodDetail = ob.FindProperty("curLodDetail");
//			if (Application.isPlaying) {
//				EditorGUILayout.PropertyField(curLodDetail, new GUIContent("Current Level of Detail"));
//			} else {
//				EditorGUILayout.PropertyField(curLodDetail, new GUIContent("Starting Level of Detail"));
//			}
//
//			if (curLodDetail.floatValue > 1000)
//				curLodDetail.floatValue = 1000;
//			else if (curLodDetail.floatValue < 0.1f)
//				curLodDetail.floatValue = 0.1f;
//			--EditorGUI.indentLevel;
//		}

		// do substances
		doSubstancesGUI(ob);


		// show statistics
		showStatistics = doBigFoldout(showStatistics, "Statistics");
		if (showStatistics) {
			EditorGUILayout.LabelField("Chunk Count: " + editor.renderers.Count);
			doTreeSizeGUI(editor);
		}
	}

	protected void doGenerationGUI(Vox.VoxelEditor editor) {

		GUILayout.Label ("Properties", labelBigFont);
		doTreeSizeGUI(generationParameters);

		// general properties
		doGeneralPropertiesGUI(generationParameters);

		// substances
        doSubstancesGUI(ob);

        // generation mode
        GUILayout.Label("Generation Mode", labelBigFont);
		selectedGenerationMode = GUILayout.Toolbar(selectedGenerationMode, generationModes);
		switch(selectedGenerationMode) {
		case 0:
			doFlatGenerationGUI();
			break;
		case 1:
			doSphereGenerationGUI();
			break;
		case 2:
			doProceduralGenerationGUI();
			break;
		case 3:
			doHeightmapGenerationGUI();
			break;
		}

		// confirmation
		GUILayout.Label ("Confirmation", labelBigFont);
		if (GUILayout.Button("Generate", buttonBigFont) && validateSubstances(editor)) {
			if (EditorUtility.DisplayDialog("Generate Voxels?", "Are you sure you want to generate the voxel terain from scratch?  Any previous work will be overriden.", "Generate", "Cancel")) {
				generateVoxels(editor);
			}
		}
		if (GUILayout.Button("Cancel Generation", buttonBigFont)) {
			setupGeneration = false;
		}
		EditorGUILayout.Separator();
	}

	protected void doTreeSizeGUI(Vox.VoxelEditor editor) {
		// world detail
		EditorGUILayout.LabelField("Voxel Power", editor.maxDetail.ToString());

		long dimension = 1 << editor.maxDetail;
		++EditorGUI.indentLevel;
		EditorGUILayout.LabelField("Voxels Per Side", dimension.ToString(numFormInt));
		EditorGUILayout.LabelField("Max Voxel Count", Mathf.Pow(dimension, 3).ToString(numFormInt));
		--EditorGUI.indentLevel;
		EditorGUILayout.Separator();

		// world dimension
		EditorGUILayout.LabelField("World Size (m)", editor.baseSize.ToString());
		++EditorGUI.indentLevel;
		EditorGUILayout.LabelField("World Area", Mathf.Pow(editor.baseSize / 1000, 2).ToString(numForm) + " square km");
		EditorGUILayout.LabelField("World Volume", Mathf.Pow(editor.baseSize / 1000, 3).ToString(numForm) + " cubic km");
		--EditorGUI.indentLevel;
		EditorGUILayout.Separator();

		EditorGUILayout.LabelField("Voxel Size", (editor.baseSize / dimension).ToString(numForm) + " m");
		EditorGUILayout.Separator();
	}

	protected void doTreeSizeGUI(VoxelEditorParameters editor) {
		// world detail
		int maxDetail = EditorGUILayout.IntField("Voxel Power", editor.maxDetail);
		if (maxDetail > 16)
			maxDetail = 16;
		else if (maxDetail < 4)
			maxDetail = 4;
		editor.maxDetail = (byte)maxDetail;

		long dimension = 1 << editor.maxDetail;
		++EditorGUI.indentLevel;
		EditorGUILayout.LabelField("Voxels Per Side", dimension.ToString(numFormInt));
		EditorGUILayout.LabelField("Max Voxel Count", Mathf.Pow(dimension, 3).ToString(numFormInt));
		--EditorGUI.indentLevel;
		EditorGUILayout.Separator();

		// world dimension
		editor.baseSize = EditorGUILayout.FloatField("World Size (m)", editor.baseSize);
		if (editor.baseSize < 0)
			editor.baseSize = 0;
		++EditorGUI.indentLevel;
		EditorGUILayout.LabelField("World Area", Mathf.Pow(editor.baseSize / 1000, 2).ToString(numForm) + " square km");
		EditorGUILayout.LabelField("World Volume", Mathf.Pow(editor.baseSize / 1000, 3).ToString(numForm) + " cubic km");
		--EditorGUI.indentLevel;
		EditorGUILayout.Separator();

		EditorGUILayout.LabelField("Voxel Size", (editor.baseSize / dimension).ToString(numForm) + " m");
		EditorGUILayout.Separator();
	}

	protected void doSubstancesGUI(SerializedObject ob) {
		SerializedProperty voxelSubstances = ob.FindProperty("voxelSubstances");
		showSubstances = doBigFoldout(showSubstances, "Substances");
		if (showSubstances)
			InspectorList.doArrayGUISimple(ref voxelSubstances);
		ob.ApplyModifiedProperties();
	}

	protected void doGeneralPropertiesGUI(Vox.VoxelEditor editor) {
		editor.createColliders = EditorGUILayout.Toggle(new GUIContent("Generate Colliders"), editor.createColliders);
		editor.useStaticMeshes = EditorGUILayout.Toggle(new GUIContent("Use Static Meshes"), editor.useStaticMeshes);
        // if (createColliders != editor.createColliders || useStaticMeshes != editor.useStaticMeshes) {
		// 	editor.createColliders = createColliders;
		// 	editor.useStaticMeshes = useStaticMeshes;
        //     editor.generateRenderers();
        // }
    }

	protected void doGeneralPropertiesGUI(VoxelEditorParameters editor) {
		editor.createColliders = EditorGUILayout.Toggle(new GUIContent("Generate Colliders"), editor.createColliders);
		editor.useStaticMeshes = EditorGUILayout.Toggle(new GUIContent("Use Static Meshes"), editor.useStaticMeshes);
	}

	protected void doFlatGenerationGUI() {
		generationParameters.heightPercentage = System.Math.Max(System.Math.Min(
			EditorGUILayout.FloatField("Height Percentage", generationParameters.heightPercentage), 100f), 0f);
	}

	protected void doSphereGenerationGUI() {
		generationParameters.spherePercentage = System.Math.Max(System.Math.Min(
			EditorGUILayout.FloatField("Sphere Radius Percentage", generationParameters.spherePercentage), 100f), 0f);
    }

	protected void doProceduralGenerationGUI() {
		generationParameters.maxChange = EditorGUILayout.FloatField("Roughness", generationParameters.maxChange);
		if (generationParameters.maxChange > 5)
			generationParameters.maxChange = 5;
		else if (generationParameters.maxChange < 0.01f)
			generationParameters.maxChange = 0.01f;
        generationParameters.proceduralSeed = EditorGUILayout.IntField("Random Seed", generationParameters.proceduralSeed);
    }

    protected void doHeightmapGenerationGUI() {
		SerializedProperty heightmaps = ob.FindProperty("heightmaps");
		EditorGUILayout.PropertyField(heightmaps, new GUIContent("Height Maps"), true);
		SerializedProperty heightmapSubstances = ob.FindProperty("heightmapSubstances");
		EditorGUILayout.PropertyField(heightmapSubstances, new GUIContent("Height Map Substances"), true);
    }

    public void generateVoxels(Vox.VoxelEditor editor) {
        editor.wipe();
        generationParameters.setTo(editor);
        editor.initialize();
        switch (selectedGenerationMode) {
        case 0:
            editor.setToHeight();
            break;
        case 1:
			editor.setToSphere();
            break;
        case 2:
            editor.setToProcedural();
            break;
		case 3:
			editor.heightmaps = generationParameters.heightmaps;
			editor.heightmapSubstances = generationParameters.heightmapSubstances;
			editor.setToHeightmap();
			break;
        }
        editor.generateRenderers();
        setupGeneration = false;
    }

    protected static void applyBrush(Vox.VoxelEditor editor, Ray mouseLocation) {
		byte opacity = byte.MaxValue;
		if (UnityEngine.Event.current.shift) {
			opacity = byte.MinValue;
		}
        Vector3 point = editor.getBrushPoint(mouseLocation);
        switch(editor.selectedBrush) {
		case 0:
			Vox.SphereModifier sphereMod = new Vox.SphereModifier(editor, point, editor.sphereBrushSize, new Vox.Voxel(editor.sphereBrushSubstance, opacity), true);
			sphereMod.overwriteShape = !editor.sphereSubstanceOnly;
			sphereMod.apply();
			break;
		case 1:
			Vox.CubeModifier cubeMod = new Vox.CubeModifier(editor, point, editor.cubeBrushDimensions, new Vox.Voxel(editor.cubeBrushSubstance, opacity), true);
			cubeMod.overwriteShape = !editor.cubeSubstanceOnly;
			cubeMod.apply();
			break;
		case 2:
			Vox.BlurModifier blur = new Vox.BlurModifier(editor, point, editor.smoothBrushSize, editor.smoothBrushStrength, true);
			blur.blurRadius = editor.smoothBrushBlurRadius;
			blur.apply();
			break;
		}
	}

	protected bool validateSubstances(Vox.VoxelEditor editor) {
		if (editor.voxelSubstances == null || editor.voxelSubstances.Length < 1) {
			EditorUtility.DisplayDialog("Invalid Substances", "There must be at least one voxel substance defined to generate the voxel object.", "OK");
			return false;
		}
		int i = 1;
		foreach(Vox.VoxelSubstance sub in editor.voxelSubstances) {
			if (sub.renderMaterial == null || sub.blendMaterial == null) {
				EditorUtility.DisplayDialog("Invalid Substance", "Substance " +i +", " +sub.name +", must have a render material and a blend material set.", "OK");
				return false;
			}
			++i;
		}
		return true;
	}

    protected class VoxelEditorParameters {
		public float baseSize = 32;
		public byte maxDetail = 6;
		// public byte isoLevel = 127;
		// public float lodDetail = 1;
		// public bool useLod = false;
		// public GameObject trees;
		// public float treeDensity = 0.02f;
		// public float treeSlopeTolerance = 5;
		// public float curLodDetail = 10f;
		public float spherePercentage;
		public float heightPercentage;
		public float maxChange;
        public int proceduralSeed;
        public bool createColliders = true;
		public bool useStaticMeshes = true;
		public Texture2D[] heightmaps;
		public byte[] heightmapSubstances;

        public void setFrom(Vox.VoxelEditor editor) {
            baseSize = editor.baseSize;
            maxDetail = editor.maxDetail;
            maxChange = editor.maxChange;
            proceduralSeed = editor.proceduralSeed;
            createColliders = editor.createColliders;
            useStaticMeshes = editor.useStaticMeshes;
			heightPercentage = editor.heightPercentage;
			spherePercentage = editor.spherePercentage;
        }

		public void setTo(Vox.VoxelEditor editor) {
			editor.baseSize = baseSize;
            editor.maxDetail = maxDetail;
            editor.createColliders = createColliders;
            editor.useStaticMeshes = useStaticMeshes;
			editor.heightPercentage = heightPercentage;
			editor.spherePercentage = spherePercentage;
		}
	}

	protected bool doBigFoldout(bool foldedOut, string label) {
		return EditorGUI.Foldout(GUILayoutUtility.GetRect(new GUIContent(label), foldoutBigFont), foldedOut, label, true, foldoutBigFont);
	}

	protected float doSliderFloatField(string label, float value, float min, float max) {
		GUILayout.BeginHorizontal();
		GUILayout.Label(label, GUILayout.ExpandWidth(false));
		float newValue = value;
		newValue = GUILayout.HorizontalSlider(newValue, min, max);
		newValue = Mathf.Max(Mathf.Min(EditorGUILayout.FloatField(newValue, GUILayout.MaxWidth(64)), max), min);
		GUILayout.EndHorizontal();
		return newValue;
	}

}
