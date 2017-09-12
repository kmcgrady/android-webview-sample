
using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine.UI;

[RequireComponent(typeof(Renderer))]
public class AndroidWebview : MonoBehaviour {

    #region Public properties

    [Tooltip("Default URL to load, you can load a new URL with LoadUrl(String url)")]
    public string url = "https://jerome.gangneux.net/projects/2017-02-android-vr-webview/";

    [Tooltip("Website width")]
    public int websiteWidth = 1024;

    [Tooltip("Website height")]
    public int websiteHeight = 1024;

    [Tooltip("Set to true if you want to send event to the Native Webview")]
    public bool propagateHitEvent = true;

    [Tooltip("This is only to show that delegation works, it is optional")]
    public Text statusText = null;

    [Tooltip("In Editor mode debug is always true, please set to false when release your App.")]
    public bool isDebug = false;

    #endregion
    #region Internals

    protected Texture2D nativeTexture = null;
    protected IntPtr    nativeTexId   = IntPtr.Zero;
    protected AndroidJavaObject view  = null;

    /**
     * You need a Renderer with a texture (best result with unlit texture)
     */ 
    protected Renderer mainRenderer = null;

    public enum ActionEvent {
        Down   = 1,
        Up     = 2,
        Move   = 3,
        Cancel = 4,
        Max
    }

    protected enum SurfaceEvent {
        Init   = 0,
        Stop   = 1,
        Update = 2,
        Max
    };

    #endregion
    #region State

    protected bool isClickedDown = false;
    protected bool isScrolling   = false;

    #endregion
    #region Unity Lifecycle

    /**
     * Used to configure the renderer and the external texture
     */
    void Awake () {
        
#if UNITY_EDITOR        
        isDebug = true;
        Debug.LogWarning ("WARNING: This plugin DOES NOT work on the editor");
#else
        JRMGX_Android_View_Init ();

        mainRenderer = GetComponent<Renderer> ();
        if (mainRenderer.material == null) {
            Debug.LogError ("No material for surface");
        }

        Texture2D texture = new Texture2D (websiteWidth, websiteHeight, TextureFormat.RGBA32, true, false);
        IntPtr i = texture.GetNativeTexturePtr ();
        nativeTexture = Texture2D.CreateExternalTexture (websiteWidth, websiteHeight, TextureFormat.RGBA32, true, false, i);

        IssuePluginEvent (SurfaceEvent.Init);
#endif
    }

    /**
     * Start the rendering of the Webview
     */
    void Start() {
#if !UNITY_EDITOR
        StartCoroutine (DelayedStart());
#endif
    }

    /**
     * Check each frame if the Webview has a new update
     */
    void Update () {
#if !UNITY_EDITOR
        IntPtr currTexId = JRMGX_Android_View_GetTexture ();
        if (currTexId != nativeTexId) {
            nativeTexId = currTexId;
            nativeTexture.UpdateExternalTexture (currTexId);
            DebugMessage ("AndroidView - texture size is " + nativeTexture.width + " x " + nativeTexture.height);
        }

        IssuePluginEvent (SurfaceEvent.Update);
        HandleInteraction ();
#endif
    }

    /**
     * EDIT ME
     * =======
     * This is a good example of an user interaction based on the Touch input (non VR mode)
     * You can adapt this method with any raycast you want
     */
    void HandleInteraction () {
        if (Input.touchCount > 0) {
            Touch touch = Input.GetTouch (0);
            Vector3 position = new Vector3 (touch.position.x, touch.position.y, 0);

            ActionEvent actionEvent = ActionEvent.Move;
            if (touch.phase == TouchPhase.Began) {
                actionEvent = ActionEvent.Down;
            } else if (touch.phase == TouchPhase.Ended) {
                actionEvent = ActionEvent.Up;
            }

            // Raycast from the Main Camera to screen position
            Ray cast = Camera.main.ScreenPointToRay (position);
            RaycastHit hit;
            if (Physics.Raycast (cast, out hit)) {
                GameObject touched = hit.collider.gameObject;
                Vector2 local = CalculatePositionFrom (hit);
                SendEvent (local, actionEvent);
            }
        }
    }

    /**
     * Ask the Webview to consume waiting event
     */
    void LateUpdate () {
#if !UNITY_EDITOR
        if (propagateHitEvent == false) return;

        if (view != null) {
            view.Call ("eventPoll");
        }
#endif
    }

    void OnApplicationQuit () {
#if !UNITY_EDITOR
        IssuePluginEvent (SurfaceEvent.Stop);
#endif
    }

    #endregion
    #region Public interface

    /**
     * Webview method goBack
     * Load the previous page if possible
     */
    public void GoBack () {
        DebugMessage ("GoBack()");
#if !UNITY_EDITOR
        view.Call ("goBack");
#endif
    }

    /**
     * Webview method goForward
     * Load the next page if possible
     */
    public void GoForward () {
        DebugMessage ("GoForward()");
#if !UNITY_EDITOR
        view.Call ("goForward");
#endif       
    }

    /**
     * Webview method loadUrl
     * Load a new Url in the webview
     */
    public void LoadUrl (String newUrl) {
        url = newUrl;
        DebugMessage ("LoadUrl(" + newUrl + ")");
#if !UNITY_EDITOR
        view.Call ("loadUrl", newUrl);
#endif
    }

    /**
     * Webview method loadData
     * Load a data uri in the webview
     */
    public void LoadData (String data) {
        DebugMessage ("LoadData(" + data + ")");
#if !UNITY_EDITOR
        view.Call ("loadData", data);
#endif
    }

    /**
     * Webview method evaluateJavascript
     * Execute Javascript code in the context of the webview
     * If you need the result of this execution you can check the delegate message `onResultEvaluateJavascript:string`
     */
    public void EvaluateJavascript (String script) {
        DebugMessage ("EvaluateJavascript(" + script + ")");
#if !UNITY_EDITOR
        view.Call ("evaluateJavascript", script);
#endif
    }

    /**
     * Webview method setUserAgent
     * Set the current Webview user agent to that String
     */
    public void SetUserAgent (String userAgent) {
        DebugMessage ("SetUserAgent(" + userAgent + ")");
#if !UNITY_EDITOR
        view.Call ("setUserAgent", userAgent);
#endif
    }

    /**
     * This is how you can send events to the Webview
     * position is the final pointer position on the underlying 2D Webview
     * It is android coordinate system: top,left = 0,0 / bottom,right = textureHeight,textureWidth
     * See the `Event system` section below for implementation example
     */
    public void SendEvent (Vector2 position, ActionEvent actionType) {
        if (propagateHitEvent == false) return;
        DebugMessage ("Send event to Webview: Type=" + actionType.ToString () + " (" + (int) position.x + "," + (int) position.y + ")");
#if !UNITY_EDITOR        
        if (view != null) {
            view.Call ("eventAdd", (int) position.x, (int) position.y, (int) actionType);
        } 
        else {
            Debug.LogError ("WARNING: Send Event view is null");
        }
#endif
    }

    #endregion
    #region Delegation

    /**
     * EDIT ME
     * =======
     * 
     * The Webview will give you delegate messages for specific events
     * message:
     *  - onPageStarted:string              <-- when a new url is about to be loaded
     *  - onPageFinished:string             <-- when a new url has finished to load
     *  - onReceivedError:string            <-- when an error happen
     *  - onResultEvaluateJavascript:string <-- result of a EvaluateJavascript call  
     * you need to parse these messages and handle it as you wish
     */
    protected void Delegation (string message) {
        // Do something here with `message`
        DebugMessage ("Got delegate message: " + message);
        if (statusText != null) {
            statusText.text = message + "\n" + statusText.text;
        }
    }
       
    #endregion
    #region Event system
        
    /**
     * Convert an Unity `RaycastHit` position to the webview coordinate position
     */
    public Vector2 CalculatePositionFrom (RaycastHit hit) {
        Vector3 position = transform.InverseTransformPoint (hit.point);
        float x =  (position.x + 0.5f) * websiteWidth;
        float y = -(position.y - 0.5f) * websiteHeight;
        return new Vector2 (x, y);
    }
        
    #endregion
        
    // Internal to plugin
    #region Please do not change anything from here

    protected void DebugMessage (string message) {
        if (isDebug) {
            Debug.Log ("JrmgxAndroidWebview: " + message);
        }
    }

#if !UNITY_EDITOR

    protected IEnumerator DelayedStart () {
        yield return null; // skip 1 frame to allow Init from the plugin

        view = StartOnTextureId (websiteWidth, websiteHeight);
        mainRenderer.material.mainTexture = nativeTexture;
    }

    protected AndroidJavaObject StartOnTextureId (int texWidth, int texHeight) {

        JRMGX_Android_View_SetTextureDetails (websiteWidth, websiteHeight);

        IntPtr androidSurface = JRMGX_Android_View_GetObject ();
        AndroidJavaObject javaObject = new AndroidJavaObject ("net.gangneux.dev.jrmgxlibview.JrmgxWebView", texWidth, texHeight, isDebug, 0);
        IntPtr setSurfaceMethodId = AndroidJNI.GetMethodID (javaObject.GetRawClass(), "setSurface", "(Landroid/view/Surface;)V");
        jvalue[] parms = new jvalue[1]; parms[0] = new jvalue(); parms[0].l = androidSurface;

        try {
            AndroidJNI.CallVoidMethod (javaObject.GetRawObject (), setSurfaceMethodId, parms);
            javaObject.Call ("loadUrl", url);
            javaObject.Call ("setDelegate", gameObject.name, "Delegation");
        } catch (Exception e) {
            Debug.LogError ("AndroidView Failed to start webview with message " + e.Message);
        }

        return javaObject;
    }

    protected static void IssuePluginEvent (SurfaceEvent eventType) {
        //GL.IssuePluginEvent (IntPtr.Zero, (int) eventType);
        GL.IssuePluginEvent ((int) eventType);
    }
        
    // These are the native methods imported from the plugin 
    [DllImport("JrmgxAndroidView")]
    protected static extern void JRMGX_Android_View_Init ();

    [DllImport("JrmgxAndroidView")]
    protected static extern IntPtr JRMGX_Android_View_GetObject ();

    [DllImport("JrmgxAndroidView")]
    protected static extern IntPtr JRMGX_Android_View_GetTexture ();

    [DllImport("JrmgxAndroidView")]
    protected static extern void JRMGX_Android_View_SetTextureDetails (int texWidth, int texHeight);

#else

    protected IEnumerator DelayedStart () {
        yield return null; // skip 1 frame to allow Init from the plugin
        mainRenderer.material.mainTexture = nativeTexture;
    }

    protected AndroidJavaObject StartOnTextureId (int texWidth, int texHeight) {
        Debug.Log ("StartOnTextureId (int " + texWidth + ", int " + texHeight + ")");
        return null;
    }

    protected static void IssuePluginEvent (SurfaceEvent eventType) { }

    protected static void JRMGX_Android_View_Init () { }

    protected static IntPtr JRMGX_Android_View_GetObject () {
        return IntPtr.Zero;
    }
        
    protected static IntPtr JRMGX_Android_View_GetTexture () {
        return IntPtr.Zero;
    }
        
    protected static void JRMGX_Android_View_SetTextureDetails (int texWidth, int texHeight) {
        Debug.Log ("JRMGX_Android_View_SetTextureDetails (int " + texWidth + ", int " + texHeight + ")");
    }

#endif
    #endregion
}
