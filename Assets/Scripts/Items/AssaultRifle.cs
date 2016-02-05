using UnityEngine;
using System.Collections;
using System;

public class AssaultRifle : Item {

	public float fireDelay = 0.1f;
	public float inaccuracy = 0.1f;
	public float range = 30;
	public float damage = 10;
	public float impulse = 1;

	public override void invoke(Inventory invoker) {
		Transform cam = invoker.getPlayer().cam.transform;

		print("firing'!");
		RaycastHit hitInfo;
		bool hit = Physics.Raycast(cam.position, cam.forward, out hitInfo, range);
		if (hit) {
			Rigidbody rb = hitInfo.collider.GetComponent<Rigidbody>();
            if (rb != null) {
				print("huh?");
				rb.AddForceAtPosition(cam.forward * impulse, hitInfo.point);
			}
		}
	}
}
