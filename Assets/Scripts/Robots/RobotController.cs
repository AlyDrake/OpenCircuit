﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

[AddComponentMenu("Scripts/Robot/Robot Controller")]
public class RobotController : MonoBehaviour, ISerializationCallbackReceiver {

	[SerializeField]
	public byte[] serializedData;

	private HashSet<Endeavour> availableEndeavours = new HashSet<Endeavour> (new EndeavourComparer());
	private List<LabelHandle> trackedTargets = new List<LabelHandle> ();
	private AudioSource soundEmitter;

#if UNITY_EDITOR
	public bool debug = false;
	public bool shouldAlphabetize = false;
	//Used for debugging sound
	//[System.NonSerialized]
	//public List<GameObject> trackedObjects = new List<GameObject>();
#endif

	public Label[] locations;
    public Goal[] goals;
	[System.NonSerialized]
	public InherentEndeavourFactory[] inherentEndeavours = new InherentEndeavourFactory[0];
		[System.NonSerialized]
	public Dictionary<GoalEnum, Goal> goalMap = new Dictionary<GoalEnum, Goal>();

    public float reliability = 5f;
	public AudioClip targetSightedSound;

	[System.NonSerialized]
	private HashSet<Endeavour> currentEndeavours = new HashSet<Endeavour>();
	[System.NonSerialized]
	private Dictionary<System.Type, AbstractRobotComponent> componentMap = new Dictionary<System.Type, AbstractRobotComponent> ();

	[System.NonSerialized]
	MentalModel mentalModel = new MentalModel ();
	[System.NonSerialized]
	MentalModel externalMentalModel = null;

	[System.NonSerialized]
	Queue<RobotMessage> messageQueue = new Queue<RobotMessage>();

	[System.NonSerialized]
	private bool dirty = false;

	[System.NonSerialized]
	private float timeoutSeconds = 10;



	void Start() {
		soundEmitter = gameObject.AddComponent<AudioSource>();
        foreach(Goal goal in goals) {
            if(!goalMap.ContainsKey(goal.type)) {
                goalMap.Add(goal.type, goal);
            }
        }
		AbstractRobotComponent [] compenents = GetComponentsInChildren<AbstractRobotComponent> ();

		foreach (AbstractRobotComponent component in compenents) {
			componentMap[component.getComponentArchetype()] = component;
		}

		foreach (Label location in locations) {
			if (location == null) {
				Debug.LogWarning("Null location attached to AI with name: " + gameObject.name);
				continue;
			}
			mentalModel.addSighting(location.labelHandle, location.transform.position, null);
			trackTarget(location.labelHandle);
		}
		InvokeRepeating ("evaluateActions", .1f, .2f);
	}

	// Update is called once per frame
	void Update () {

//Leave this here, very useful!!
//#if UNITY_EDITOR

//		if(debug) {
//			/* 
//			* Draw tracked targets
//			*/
//			foreach(GameObject obj in trackedObjects) {
//				Destroy(obj);
//			}
//			trackedObjects.Clear();
//			foreach(LabelHandle handle in trackedTargets) {
//				GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
//				capsule.transform.position = handle.getPosition();
//				capsule.GetComponent<MeshRenderer>().material.color = Color.yellow;
//				capsule.transform.localScale = new Vector3(.2f, .2f, .2f);
//				Destroy(capsule.GetComponent<Rigidbody>());
//				Destroy(capsule.GetComponent<CapsuleCollider>());
//				trackedObjects.Add(capsule);
//			}
//		}
//#endif
		while (messageQueue.Count > 0) {
			RobotMessage message = messageQueue.Dequeue();

			if (message.Type == RobotMessage.MessageType.TARGET_SIGHTED) {
				if(targetSightedSound != null && message.Target.getName().Equals("Player") && (!getMentalModel().knowsTarget (message.Target) || (System.DateTime.Now - getMentalModel().getLastSightingTime(message.Target).Value).Seconds > timeoutSeconds)) {
					soundEmitter.PlayOneShot(targetSightedSound);
				}
				sightingFound(message.Target, message.TargetPos, message.TargetVelocity);
				trackTarget(message.Target);
				//evaluateActions();
			} else if(message.Type == RobotMessage.MessageType.TARGET_LOST) {
				sightingLost(message.Target, message.TargetPos, message.TargetVelocity);
				trackedTargets.Remove(message.Target);
				//evaluateActions();
			}
			else if (message.Type == RobotMessage.MessageType.ACTION) {
				foreach( Endeavour action in currentEndeavours) {
					action.onMessage(message);
				}
			}
		}
		if (dirty) {
			evaluateActions();
		}
	}

	public void notify (EventMessage message){
		if (message.Type.Equals ("target found")) {
			trackTarget(message.Target);
		} else if (message.Type.Equals ("target lost")) {
			trackedTargets.Remove(message.Target);
		}
		dirty = true;
	}
	
	public void addEndeavour(Endeavour action) {
		availableEndeavours.Add (action);
		//evaluateActions ();
		dirty = true;
	}

	public void trackTarget(LabelHandle target) {
		trackedTargets.Add(target);
		target.getPosition();
		foreach(InherentEndeavourFactory factory in inherentEndeavours) {
			if(factory.isApplicable(target)) {
				Endeavour action = factory.constructEndeavour(this, target);
				if(action != null) {
					availableEndeavours.Add(action);
					dirty = true;

				}
			}
		}
		if(target.label != null) {
			foreach(Endeavour action in target.label.getAvailableEndeavours(this)) {
				availableEndeavours.Add(action);
				dirty = true;

			}
		}
	}

	public bool knowsTarget(LabelHandle target) {
		return getMentalModel ().canSee (target);
	}

	public System.Nullable<Vector3> getLastKnownPosition(LabelHandle target) {
		return getMentalModel().getLastKnownPosition(target);
	}

	public void attachMentalModel(MentalModel model) {
		externalMentalModel = model;
	}

	public void detachMentalModel () {
		externalMentalModel = null;
	}

	public void enqueueMessage(RobotMessage message) {
		messageQueue.Enqueue (message);
	}

	public Dictionary<GoalEnum, Goal> getGoals() {
		return goalMap;
	}

	public Dictionary<System.Type, AbstractRobotComponent> getComponentMap() {
		return componentMap;
	}

	public List<LabelHandle> getTrackedTargets() {
		return trackedTargets;
	}

	private void evaluateActions() {
		List<DecisionInfoObject> debugText = new List<DecisionInfoObject>();
		//print("****EVALUATE****");
		dirty = false;
		PriorityQueue endeavourQueue = new PriorityQueue();
		List<Endeavour> staleEndeavours = new List<Endeavour>();
		//print("\tCurrent Endeavours");
		foreach(Endeavour action in currentEndeavours) {
			if(action.isStale()) {
				//print("\t\tstale: " + action.getName());
				action.stopExecution();
				staleEndeavours.Add(action);
			} else {
				//print("\t\t++" + action.getName());
				availableEndeavours.Add(action);
			}
		}
		//print("\tAvailable Endeavours");
		foreach(Endeavour action in availableEndeavours) {
			if(action.isStale()) {
				//print("\t\t--" + action.getName());
				staleEndeavours.Add(action);
			} else {
				//print("\t\t++" + action.getName());
				endeavourQueue.Enqueue(action);
			}
		}

		foreach(Endeavour action in staleEndeavours) {
			//print("remove: " + action.getName());
			availableEndeavours.Remove(action);
			currentEndeavours.Remove(action);
		}
		HashSet<Endeavour> proposedEndeavours = new HashSet<Endeavour>();

		Dictionary<System.Type, int> componentMap = getComponentUsageMap();

#if UNITY_EDITOR
		bool maxPrioritySet = false;
		float localMaxPriority = 0;
		float localMinPriority = 0;
#endif

		while(endeavourQueue.Count > 0) {
			Endeavour action = (Endeavour)endeavourQueue.Dequeue();
			if(action.isReady(componentMap)) {
#if UNITY_EDITOR
				if(debug) {
					float priority = action.getPriority();
					if(!maxPrioritySet) {
						maxPrioritySet = true;
						localMaxPriority = priority;
						localMinPriority = priority;
					} else {
						if(priority > localMaxPriority) {
							localMaxPriority = priority;
						}
						if(priority < localMinPriority) {
							localMinPriority = priority;
						}
					}
					debugText.Add(new DecisionInfoObject(action.getName(), action.getParent().getName(), priority, true));
				}
#endif
				if(proposedEndeavours.Contains(action)) {
					Debug.LogError("action already proposed!!!");
				}
				proposedEndeavours.Add(action);
				availableEndeavours.Remove(action);
			} else {
#if UNITY_EDITOR
				if(debug) {
					float priority = action.getPriority();
					if(!maxPrioritySet) {
						maxPrioritySet = true;
						localMaxPriority = priority;
						localMinPriority = priority;
					} else {
						if(priority > localMaxPriority) {
							localMaxPriority = priority;
						}
						if(priority < localMinPriority) {
							localMinPriority = priority;
						}
					}
					debugText.Add(new DecisionInfoObject(action.getName(), action.getParent().getName(), priority, false));
				}
#endif
				if(action.active) {
					action.stopExecution();
				}
			}
		}

		// All previous actions MUST be stopped before we start the new ones
		foreach(Endeavour action in proposedEndeavours) {
			if(!action.active) {
				action.execute();
			}
		}
	
		currentEndeavours = proposedEndeavours;
#if UNITY_EDITOR
		if(debug) {
			lines = debugText;
			maxPriority = (Mathf.Abs(localMaxPriority) > Mathf.Abs(localMinPriority)) ? Mathf.Abs(localMaxPriority) : Mathf.Abs(localMinPriority);
			if(shouldAlphabetize) {
				alphabetize();
			}
		}
#endif
	}

	private Dictionary<System.Type, int> getComponentUsageMap() {
		Dictionary<System.Type, int> componentUsageMap = new Dictionary<System.Type, int>();
		foreach (AbstractRobotComponent component in componentMap.Values) {
			if (componentUsageMap.ContainsKey(component.getComponentArchetype())) {
				int count = componentUsageMap[component.getComponentArchetype()];
				++count;
				componentUsageMap[component.getComponentArchetype()] = count;
			}
			else {
				componentUsageMap[component.getComponentArchetype()] = 1;
			}
		}
		return componentUsageMap;
	}

	private void sightingLost(LabelHandle target, Vector3 lastKnownPos, Vector3? lastKnownVelocity) {
		if (externalMentalModel != null) {
			externalMentalModel.removeSighting(target, lastKnownPos, lastKnownVelocity);
		}
		mentalModel.removeSighting(target, lastKnownPos, lastKnownVelocity);
	}

	private void sightingFound(LabelHandle target, Vector3 pos, Vector3? dir) {
		if (externalMentalModel != null) {
			externalMentalModel.addSighting(target, pos, dir);
		}
		mentalModel.addSighting(target, pos, dir);
	}

	public MentalModel getMentalModel() {
		if (externalMentalModel == null) {
			return mentalModel;
		} else {
			return externalMentalModel; 
		}
	}

#if UNITY_EDITOR
	public List<DecisionInfoObject> lines = new List<DecisionInfoObject>();
	public float maxPriority = 0;
	public float minPriority = 0;

	void alphabetize() {
		if(lines.Count > 0) {
			List<DecisionInfoObject> newList = new List<DecisionInfoObject>();
			newList.Add(lines[0]);
			lines.RemoveAt(0);
			foreach(DecisionInfoObject obj in lines) {
				for(int i = 0; i < newList.Count; ++i) {
					if(obj.getTitle().CompareTo(newList[i].getTitle()) < 0) {
						newList.Insert(i, obj);
						break;
					} else if(i == newList.Count - 1) {
						newList.Add(obj);
						break;
					}
				}
			}
			lines = newList;
		}
	}

	void OnGUI() {
		if(debug) {
			Camera cam = Camera.current;
			Vector3 pos;
			if(cam != null) {
				Vector3 worldTextPos = transform.position + new Vector3(0, 1, 0);
				pos = cam.WorldToScreenPoint(worldTextPos);
				if(Vector3.Dot(cam.transform.forward, (worldTextPos - cam.transform.position).normalized) < 0) {
					return;
				}
			} else {
				return;
			}

			while(lines.Count > 8) {
				lines.RemoveAt(0);
			}

			GUI.enabled = true;
			string buffer = "";
			for(int i = 0; i < lines.Count; i++) {
				buffer += "\n";
			}


			Texture2D red = new Texture2D(1, 1);
			Color transparentRed = new Color(0f, .0f, .0f, .4f);

			red.SetPixel(0, 0, transparentRed);
			red.Apply();

			Texture2D blue = new Texture2D(1, 1);
			Color transparentBlue = new Color(.1f, .1f, 1f, .6f);
			blue.SetPixel(0, 0, transparentBlue);
			blue.alphaIsTransparency = true;

			blue.Apply();

			Texture2D green = new Texture2D(1, 1);
			Color transparentGreen = new Color(.1f, 1f, .1f, .4f);
			green.SetPixel(0, 0, transparentGreen);
			green.alphaIsTransparency = true;

			green.Apply();

			float lineHeight = 30f;
			Vector2 size = new Vector2(200, lines.Count * lineHeight);
			for (int i = 0; i < lines.Count; ++i) {
				DecisionInfoObject obj = lines[i];
				float percentFilled = 0;

				percentFilled = (Mathf.Abs(obj.getPriority()) / (maxPriority));
				if(obj.getPriority() < 0) {
					percentFilled = -percentFilled;
				}

				Rect rectng = new Rect(pos.x - size.x / 2, Screen.height - pos.y - size.y + (i * lineHeight), size.x, lineHeight);

				GUI.skin.box.normal.background = red;

				GUI.Box(rectng, GUIContent.none);

				Rect filled;
				if(percentFilled < 0) {
					filled = new Rect(pos.x + ((size.x / 2) * percentFilled), Screen.height - pos.y - size.y + (i * lineHeight), -((size.x / 2) * percentFilled), lineHeight);
				} else {
					filled = new Rect(pos.x, Screen.height - pos.y - size.y + (i * lineHeight), (size.x / 2) * (percentFilled), lineHeight);
				}

				//GUI.skin.box.normal.background = green;
				//GUI.Box(filled, GUIContent.none);
				GUI.DrawTexture(filled, blue);
				//if(obj.isChosen()) {
				//	GUI.Label(textCentered, "+" + obj.getTitle());
				//} else {
				//}
				Rect boxRectangle = new Rect(pos.x - size.x/2, Screen.height - pos.y - size.y + (i*lineHeight)+lineHeight/4, 15, 15);
				if(obj.isChosen()) {
					GUI.DrawTexture(boxRectangle, green);
				} else {
					GUI.DrawTexture(boxRectangle, red);
				}

				Rect textCentered = new Rect(pos.x - size.x / 2 + 17, Screen.height - pos.y - size.y + (i * lineHeight) + 4, size.x, lineHeight);
				GUI.Label(textCentered, obj.getTitle());


				Font font = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/GUI/Courier.ttf");
				GUIStyle style = new GUIStyle(GUI.skin.label);
				style.font = font;
				style.fontSize = 14;
				
				string priorityString = obj.getPriority().ToString("0.#0").PadLeft(7);
				Vector2 labelSize = style.CalcSize(new GUIContent(priorityString));
				//Rect center = new Rect(pos.x - labelSize.x/2 - 7, Screen.height - pos.y - size.y + (i * lineHeight), labelSize.x, labelSize.y);
				Rect center = new Rect(pos.x + size.x/2 - labelSize.x, Screen.height - pos.y - size.y + (i * lineHeight), labelSize.x, labelSize.y);
				GUI.Label(center, priorityString, style);

				string sourceString = obj.getSource();
				Vector2 sourceStringSize = style.CalcSize(new GUIContent(sourceString));
				Rect sourceRect = new Rect(pos.x + size.x / 2 - sourceStringSize.x, Screen.height - pos.y - size.y + (i * lineHeight) + lineHeight/2, sourceStringSize.x, sourceStringSize.y);
				GUI.Label(sourceRect, sourceString, style);
			}
			/* 
			 * Draw the battery meter
			*/
			Battery battery = GetComponentInChildren<Battery>();
			if(battery != null) {
				Rect progressBar = new Rect(pos.x - size.x / 2, Screen.height - pos.y + 3, size.x, 20);

				GUI.skin.box.normal.background = red;
				GUI.Box(progressBar, GUIContent.none);

				GUI.skin.box.normal.background = green;

				Rect progressBarFull = new Rect(pos.x - size.x / 2, Screen.height - pos.y + 3, size.x * (battery.currentCapacity / battery.maximumCapacity), 20);
				GUI.Box(progressBarFull, GUIContent.none);
			}
		}
	}
#endif

	public void OnBeforeSerialize() {
		lock(this) {

			MemoryStream stream = new MemoryStream();
			BinaryFormatter formatter = new BinaryFormatter();
			formatter.Serialize(stream, inherentEndeavours);

			serializedData = stream.ToArray();
			stream.Close();
		}
	}

	public void OnAfterDeserialize() {
		lock(this) {
			MemoryStream stream = new MemoryStream(serializedData);
			BinaryFormatter formatter = new BinaryFormatter();
			inherentEndeavours = (InherentEndeavourFactory[])formatter.Deserialize(stream);

			foreach(InherentEndeavourFactory factory in inherentEndeavours) {
				if(factory != null) {
					if(factory.goals == null) {
						factory.goals = new List<Goal>();
					}
				}
			}
			stream.Close();
		}


		}	


}