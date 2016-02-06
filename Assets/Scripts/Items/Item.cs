using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public abstract class Item : MonoBehaviour {

    public Texture2D icon;

    protected float reachDistance = 3f;

	// void Awake() {
	// 	gameObject.AddComponent<pickupEvent>().item = this;
	// }

	// public virtual void onPickup(InteractTrigger e) {
	// 	Player.getInstance().carryPickup(gameObject, e.getPoint());
	// }

	public abstract void beginInvoke(Inventory invoker);

	public virtual void endInvoke(Inventory invoker) {}

    public virtual void onTake(Inventory taker) {
        transform.SetParent(taker.transform);
        gameObject.SetActive(false);
    }

    public virtual void onDrop(Inventory taker) {
        transform.SetParent(null);
        gameObject.SetActive(true);
		endInvoke(taker);
    }

    public virtual void onEquip(Inventory equipper) {
    }

    public virtual void onUnequip(Inventory equipper) {
		endInvoke(equipper);
    }

    // protected virtual void trigger() {
	// 	RaycastHit col = reach();
	// 	if (col.collider != null) {
	// 		Label evH = col.collider.GetComponent<Label>();
	// 		if (evH != null) {
	// 			evH.sendTrigger(gameObject, buildTrigger());
	// 		}
	// 	}
	// }

	protected RaycastHit reach() { Vector3 fake; return reach(out fake); }
	protected RaycastHit reach(out Vector3 position) {
		RaycastHit finalHit = new RaycastHit();
		position = Vector3.zero;
		Ray ray = new Ray (this.transform.parent.position, this.transform.parent.forward);
		RaycastHit[] hits = Physics.RaycastAll(ray, reachDistance);
		float distance = reachDistance;

		foreach(RaycastHit hit in hits) {
			if (hit.distance > distance) continue;
			if (hit.collider.isTrigger) continue;
			finalHit = hit;
			distance = hit.distance;
			position = hit.point;
		}
		return finalHit;
	}
}

// [System.Serializable]
// public class PickupOperation : Operation {
//
// 	private static System.Type[] triggers = new System.Type[] {
// 		typeof(InteractTrigger),
// 	};
//
// 	public Item item;
//
// 	public override System.Type[] getTriggers() {
// 		return triggers;
// 	}
//
// 	public override void perform(GameObject instigator, Trigger trig) {
// 		InteractTrigger trigger = (InteractTrigger)trig;
// 		item.onPickup(trigger);
// 	}
// }
