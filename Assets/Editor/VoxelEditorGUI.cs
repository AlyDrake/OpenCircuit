﻿using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;


//[System.Serializable]
//public class VoxelEditorGUI {

//	//int size = 256;
//	//int power = 8;
//	string name;
//	int amount = 1;
//	int unit;

//}


[CustomEditor(typeof(Vox.VoxelEditor))]
public class VoxelEditorGUI : Editor {

	private const string numForm = "##,0.000";
	private const string numFormInt = "##,#";

	private SerializedObject ob;
	//private bool materialsFoldout = false;
	//private List<Vox.VoxelMaterial> voxelMaterials;
	protected int selectedMode = 0;
	protected String[] modes = new String[] {"Manage", "Sculpt"};

	public void OnEnable() {
		ob = new SerializedObject(target);
	}

	public override void OnInspectorGUI() {
		//Vox.VoxelEditor editor = (Vox.VoxelEditor)target;
		ob.Update();
		
		selectedMode = drawTabs(modes, selectedMode);

		if (selectedMode != 0)
			return;
		
		// world detail
		SerializedProperty maxDetail = ob.FindProperty("maxDetail");
		EditorGUILayout.PropertyField(maxDetail, new GUIContent("Voxel Power"));
		if (maxDetail.intValue > byte.MaxValue)
			maxDetail.intValue = byte.MaxValue;
		else if (maxDetail.intValue < 4)
			maxDetail.intValue = 4;

		long dimension = 1 << maxDetail.intValue;
		++EditorGUI.indentLevel;
		EditorGUILayout.LabelField("Voxels Per Side", dimension.ToString(numFormInt));
		EditorGUILayout.LabelField("Max Voxel Count", Mathf.Pow(dimension, 3).ToString(numFormInt));
		--EditorGUI.indentLevel;
		EditorGUILayout.Separator();

		// world dimension
		SerializedProperty baseSize = ob.FindProperty("BaseSize");
		EditorGUILayout.PropertyField(baseSize, new GUIContent("WorldSize (m)"));
		if (baseSize.floatValue < 0)
			baseSize.floatValue = 0;
		++EditorGUI.indentLevel;
		EditorGUILayout.LabelField("World Area", Mathf.Pow(baseSize.floatValue / 1000, 2).ToString(numForm) + " square km");
		EditorGUILayout.LabelField("World Volume", Mathf.Pow(baseSize.floatValue / 1000, 3).ToString(numForm) + " cubic km");
		--EditorGUI.indentLevel;
		EditorGUILayout.Separator();

		EditorGUILayout.LabelField("Voxel Size", (baseSize.floatValue / dimension).ToString(numForm) + " m");
		EditorGUILayout.Separator();

		// LOD
		SerializedProperty useLod = ob.FindProperty("useLod");
		EditorGUILayout.PropertyField(useLod, new GUIContent("Use Level of Detail"));
		if (useLod.boolValue) {
			++EditorGUI.indentLevel;
			SerializedProperty lodDetail = ob.FindProperty("lodDetail");
			EditorGUILayout.PropertyField(lodDetail, new GUIContent("Target Level of Detail"));
			if (lodDetail.floatValue > 1000)
				lodDetail.floatValue = 1000;
			else if (lodDetail.floatValue < 0.1f)
				lodDetail.floatValue = 0.1f;
			
			SerializedProperty curLodDetail = ob.FindProperty("curLodDetail");
			if (Application.isPlaying) {
				EditorGUILayout.PropertyField(curLodDetail, new GUIContent("Current Level of Detail"));
			} else {
				EditorGUILayout.PropertyField(curLodDetail, new GUIContent("Starting Level of Detail"));
			}

			if (curLodDetail.floatValue > 1000)
				curLodDetail.floatValue = 1000;
			else if (curLodDetail.floatValue < 0.1f)
				curLodDetail.floatValue = 0.1f;
			--EditorGUI.indentLevel;
		}

		EditorGUILayout.Separator();

		// materials

		SerializedProperty voxelMaterials = ob.FindProperty("voxelMaterials");
		EditorGUILayout.PropertyField(voxelMaterials, new GUIContent("Voxel Materials"), true);

		// procedural stats
		SerializedProperty useHeightmap = ob.FindProperty("useHeightmap");
		EditorGUILayout.PropertyField(useHeightmap, new GUIContent("Use Height Map"));
		if (useHeightmap.boolValue) {
			SerializedProperty heightmaps = ob.FindProperty("heightmaps");
			EditorGUILayout.PropertyField(heightmaps, new GUIContent("Height Maps"), true);
			SerializedProperty heightmapMaterials = ob.FindProperty("heightmapMaterials");
			EditorGUILayout.PropertyField(heightmapMaterials, new GUIContent("Height Map Materials"), true);
			SerializedProperty materialMap = ob.FindProperty("materialMap");
			EditorGUILayout.PropertyField(materialMap, new GUIContent("Material Map"));
		} else {
			SerializedProperty maxChange = ob.FindProperty("maxChange");
			EditorGUILayout.PropertyField(maxChange, new GUIContent("Roughness"));
			if (maxChange.floatValue > 5)
				maxChange.floatValue = 5;
			else if (maxChange.floatValue < 0.01f)
				maxChange.floatValue = 0.01f;
		}
		SerializedProperty createColliders = ob.FindProperty("createColliders");
		EditorGUILayout.PropertyField(createColliders, new GUIContent("Generate Colliders"));
		EditorGUILayout.Separator();



		// generation
		Vox.VoxelEditor editor = (Vox.VoxelEditor)target;
		editor.Update();
		if (GUILayout.Button("Generate")) {
			editor.wipe();
			editor.init();
			if (!EditorUtility.DisplayDialog("Voxel Generation Complete", "Voxel generation completed successfully.  Do you wish to clear the new generation?", "No", "Yes")) {
				editor.wipe();
			}
		}
		if (editor.getHead() != null || editor.chunks.Count > 0) {
			if (GUILayout.Button("Clear")) {
				editor.wipe();
			}
		}
		EditorGUILayout.LabelField("Chunk Count: " + editor.chunks.Count);
		EditorGUILayout.Separator();



		// finally, apply the changes
		ob.ApplyModifiedProperties();
	}

	public void OnSceneGUI() {
		if (selectedMode != 1)
			return;
		int controlId = GUIUtility.GetControlID(FocusType.Passive);
		switch(Event.current.GetTypeForControl(controlId)) {
		case EventType.MouseDown:
			if (Event.current.button == 0) {
				GUIUtility.hotControl = controlId;
				if (Event.current.shift) {
					subtractSphere(HandleUtility.GUIPointToWorldRay(Event.current.mousePosition));
				} else {
					addSphere(HandleUtility.GUIPointToWorldRay(Event.current.mousePosition));
				}
				Event.current.Use();
			}
			break;

		case EventType.MouseUp:
			if (Event.current.button == 0) {
				GUIUtility.hotControl = 0;
				Event.current.Use();
			}
			break;
		}
	}

	protected void addSphere(Ray mouseLocation) {
		Vox.VoxelEditor editor = (Vox.VoxelEditor)target;

		Vector3 point = getRayCollision(mouseLocation).point;

		new Vox.SphereModifier(editor, point, 1, new Vox.Voxel(0, byte.MaxValue), true);
	}
	
	protected void subtractSphere(Ray mouseLocation) {
		Vox.VoxelEditor editor = (Vox.VoxelEditor)target;
		
		Vector3 point = getRayCollision(mouseLocation).point;
		
		new Vox.SphereDestroyer(editor, point, 1, new Vox.Voxel(0, byte.MinValue), 1, true, true);
	}

	protected static RaycastHit getRayCollision(Ray ray) {
		RaycastHit firstHit = new RaycastHit();
		firstHit.distance = float.PositiveInfinity;
		foreach(RaycastHit hit in Physics.RaycastAll(ray)) {
			if (hit.distance < firstHit.distance) {
				firstHit = hit;
			}
		}
		return firstHit;
	}

	/// <summary>
	/// Creates tabs from buttons, with their bottom edge removed by the magic of Haxx
	/// </summary>
	/// <remarks>
	/// The line will be misplaced if other elements is drawn before this
	/// </remarks>
	/// <returns>Selected tab</returns>
	protected static int drawTabs(string[] options, int selected)
	{
		const float DarkGray = 0.4f;
		const float LightGray = 0.9f;
		const float StartSpace = 10;
		
		GUILayout.Space(StartSpace);
		Color storeColor = GUI.backgroundColor;
		Color highlightCol = new Color(LightGray, LightGray, LightGray);
		Color bgCol = new Color(DarkGray, DarkGray, DarkGray);
		
		GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
		buttonStyle.padding.bottom = 8;
		
		GUILayout.BeginHorizontal();
		{   //Create a row of buttons
			for (int i = 0; i < options.Length; ++i)
			{
				GUI.backgroundColor = i == selected ? highlightCol : bgCol;
				if (GUILayout.Button(options[i], buttonStyle))
				{
					selected = i; //Tab click
				}
			}
		} GUILayout.EndHorizontal();
		//Restore color
		GUI.backgroundColor = storeColor;
		//Draw a line over the bottom part of the buttons (ugly haxx)
		var texture = new Texture2D(1, 1);
		texture.SetPixel(0, 0, highlightCol);
		texture.Apply();
		GUI.DrawTexture(new Rect(0, buttonStyle.lineHeight + buttonStyle.border.top + buttonStyle.margin.top + StartSpace,  Screen.width, 4),texture);
		
		return selected;
	}


}
