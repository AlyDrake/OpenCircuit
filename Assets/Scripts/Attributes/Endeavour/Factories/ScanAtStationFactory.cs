﻿using UnityEngine;
using System.Collections;

[System.Serializable]
public class ScanAtStationFactory : EndeavourFactory {


	public override Endeavour constructEndeavour(RobotController controller) {
		if(parent == null) {
			return null;
		}
		return new ScanAtStationAction(controller, goals, parent.labelHandle);
	}
}
