﻿using UnityEngine;

[System.Serializable]
public class DamageOperation : Operation {
	
	private static System.Type[] triggers = new System.Type[] {
		typeof(DamageTrigger),
	};

	//public string damageType;
	//public float damageAmount;
	
	public override System.Type[] getTriggers() {
		return triggers;
	}
	
	public override void perform(GameObject instigator, Trigger trig) {
		DamageTrigger trigger = (DamageTrigger)trig;
		Player player = parent.GetComponent<Player>();
		if(player != null) {
			player.hurt(trigger.getAmount());
		}

	}

#if UNITY_EDITOR
    public override void doGUI() {
		//damageType = UnityEditor.EditorGUILayout.TextField("Type", damageType);
		//damageAmount = UnityEditor.EditorGUILayout.FloatField("Amount", damageAmount); 
	}
#endif
}
