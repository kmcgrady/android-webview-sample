
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GoButtonScript : MonoBehaviour {

    public GameObject androidWebview;
    public Text inputText;

	void Awake () {
        Button button = GetComponent<Button> ();
        button.onClick.AddListener (() => {
            androidWebview.GetComponent<AndroidWebview> ().LoadUrl (inputText.text);
        });
    }
}
