using UnityEngine;
using System.Collections;

[AddComponentMenu("Scripts/Player/Controls")]
public class Controls : MonoBehaviour {

	private Player myPlayer;
	private bool playerControlsEnabled = true;
	private Menu menu;

	public float mouseSensitivity = 1;
	public bool invertLook = false;
	public bool enableMousePadHacking = false;

	void Awake () {
		myPlayer = this.GetComponent<Player> ();
		menu = GameObject.FindGameObjectWithTag("Menu").GetComponent<Menu>();
	}

	void Update () {

		/****************MENU****************/
		if (Input.GetButtonDown("Menu")) {
			menu.toggleInGameMenu();
		}
		if (menu.paused()) {
			return;
		}

		if (inGUI() || Time.timeScale == 0 || !playerControlsEnabled)
			return;

		/****************MOVEMENT****************/
		myPlayer.mover.setForward(Input.GetAxis("Vertical"));

		myPlayer.mover.setRight(Input.GetAxis("Horizontal"));

		if (Input.GetButtonDown("Jump")) {
			myPlayer.mover.jump();
		}

		myPlayer.mover.setSprinting(Input.GetButton("Sprint"));

		myPlayer.mover.setCrouching(Input.GetButton("Crouch"));

		/****************INVENTORY***************/
		if (!myPlayer.inventory.inContext()) {
			if (enableMousePadHacking) {
				if (Input.GetButtonDown("Equip1")) {
					if (myPlayer.inventory.isSelecting())
						myPlayer.inventory.doSelect(-1);
					else
						myPlayer.inventory.doSelect(0);
				} else if (Input.GetButtonDown("Equip2")) {
					if (myPlayer.inventory.isSelecting())
						myPlayer.inventory.doSelect(-1);
					else
						myPlayer.inventory.doSelect(1);
				} else if (Input.GetButtonDown("Equip3")) {
					if (myPlayer.inventory.isSelecting())
						myPlayer.inventory.doSelect(-1);
					else
						myPlayer.inventory.doSelect(2);
				}
			} else {
				if (Input.GetButton("Equip1")) {
					myPlayer.inventory.doSelect(0);
				} else if (Input.GetButton("Equip2")) {
					myPlayer.inventory.doSelect(1);
				} else if (Input.GetButton("Equip3")) {
					myPlayer.inventory.doSelect(2);
				} else {
					myPlayer.inventory.doSelect(-1);
				}
			}
		}

		if (myPlayer.inventory.isSelecting()) {
			myPlayer.inventory.moveMouse(new Vector2(Input.GetAxis("Look Horizontal"), Input.GetAxis("Look Vertical")));
		} else {
			if (invertLook)
				myPlayer.looker.rotate(Input.GetAxis("Look Horizontal") * mouseSensitivity, -Input.GetAxis("Look Vertical") * mouseSensitivity);
			else
				myPlayer.looker.rotate(Input.GetAxis("Look Horizontal") * mouseSensitivity, Input.GetAxis("Look Vertical") * mouseSensitivity);
		}

		/****************ACTION****************/

		if (Input.GetButtonDown("Use")) {
			myPlayer.inventory.useEquipped();
		}
		if (Input.GetButtonUp("Use")) {
			myPlayer.inventory.stopUsingEquiped();
		}
		if (Input.GetButtonDown ("Interact")) {
			myPlayer.interactor.interact();
		}

//		if (Input.GetButton ("Fire2")) {
//			if (inGUI()) {
//				myPlayer.focus.unfocus ();
//			} else {
//				myPlayer.focus.focus ();
//				if (Input.GetButtonDown("Fire1")) {
//					//myPlayer.attacher.attach ();
//                    myPlayer.focus.invoke();
//				}
//			}
//		}
//		else {
//			myPlayer.focus.unfocus();
//		}
	}
	
	public void disablePlayerControls() {
		playerControlsEnabled = false;
		myPlayer.mover.setForward(0);
		myPlayer.mover.setRight(0);
	}
	
	public void enablePlayerControls() {
		playerControlsEnabled = true;
	}

	bool inGUI() {
		return false;
	}
}
