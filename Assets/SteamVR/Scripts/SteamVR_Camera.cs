﻿//========= Copyright 2014, Valve Corporation, All rights reserved. ===========
//
// Purpose: Adds SteamVR render support to existing camera objects
//
//=============================================================================

using UnityEngine;
using System.Collections;
using System.Reflection;

[RequireComponent(typeof(Camera))]
public class SteamVR_Camera : MonoBehaviour
{
	[SerializeField]
	private Transform _head;
	public Transform head { get { return _head; } }
	public Transform offset { get { return _head; } } // legacy
	public Transform origin { get { return _head.parent; } }

	public Ray GetRay()
	{
		return new Ray(_head.position, _head.forward);
	}

	public bool wireframe = false;

	[SerializeField]
	private SteamVR_CameraFlip flip;

	#region Materials

	static public Material blitMaterial;

	// Using a single shared offscreen buffer to render the scene.  This needs to be larger
	// than the backbuffer to account for distortion correction.  The default resolution
	// gives us 1:1 sized pixels in the center of view, but quality can be adjusted up or
	// down using the following scale value to balance performance.
	static public float sceneResolutionScale = 1.0f;
	static private RenderTexture _sceneTexture;
	static public RenderTexture GetSceneTexture(bool hdr)
	{
		var vr = SteamVR.instance;
		if (vr == null)
			return null;

		int w = (int)(vr.sceneWidth * sceneResolutionScale);
		int h = (int)(vr.sceneHeight * sceneResolutionScale);
		int aa = vr.graphicsAPI == Valve.VR.GraphicsAPIConvention.API_OpenGL || // MSAA disabled in OpenGL since it would render the scene all black
			QualitySettings.antiAliasing == 0 ? 1 : QualitySettings.antiAliasing;
		var format = hdr ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;

		if (_sceneTexture != null)
		{
			if (_sceneTexture.width != w || _sceneTexture.height != h || _sceneTexture.antiAliasing != aa || _sceneTexture.format != format)
			{
				Debug.Log(string.Format("Recreating scene texture.. Old: {0}x{1} MSAA={2} [{3}] New: {4}x{5} MSAA={6} [{7}]",
					_sceneTexture.width, _sceneTexture.height, _sceneTexture.antiAliasing, _sceneTexture.format, w, h, aa, format));
				Object.Destroy(_sceneTexture);
				_sceneTexture = null;
			}
		}

		if (_sceneTexture == null)
		{
			_sceneTexture = new RenderTexture(w, h, 0, format, RenderTextureReadWrite.Linear);
			_sceneTexture.antiAliasing = aa;
		}

		return _sceneTexture;
	}

	#endregion

	#region Enable / Disable

	void OnDisable()
	{
		SteamVR_Render.Remove(this);
	}

	void OnEnable()
	{
		// Bail if no hmd is connected
		var vr = SteamVR.instance;
		if (vr == null)
		{
			if (head != null)
			{
				head.GetComponent<SteamVR_GameView>().enabled = false;
				head.GetComponent<SteamVR_TrackedObject>().enabled = false;
			}

			if (flip != null)
				flip.enabled = false;

			enabled = false;
			return;
		}

		if (blitMaterial == null)
		{
			blitMaterial = new Material(Shader.Find("Custom/SteamVR_Blit"));
		}

		// Ensure rig is properly set up
		Expand();

		// Set remaining hmd specific settings
		var camera = GetComponent<Camera>();
		camera.fieldOfView = vr.fieldOfView;
		camera.aspect = vr.aspect;
		camera.eventMask = 0;			// disable mouse events
		camera.orthographic = false;	// force perspective
		camera.enabled = false;			// manually rendered by SteamVR_Render

		if (camera.actualRenderingPath != RenderingPath.Forward && QualitySettings.antiAliasing > 1)
		{
			Debug.LogWarning("MSAA only supported in Forward rendering path. (disabling MSAA)");
			QualitySettings.antiAliasing = 0;
		}

		// Ensure game view camera hdr setting matches
		var headCam = head.GetComponent<Camera>();
		if (headCam != null)
		{
			headCam.hdr = camera.hdr;
			headCam.renderingPath = camera.renderingPath;
		}

		SteamVR_Render.Add(this);
	}

	#endregion

	#region Functionality to ensure SteamVR_Camera component is always the last component on an object

	void Awake() { ForceLast(); }

	static Hashtable values;

	public void ForceLast()
	{
		if (values != null)
		{
			// Restore values on new instance
			foreach (DictionaryEntry entry in values)
			{
				var f = entry.Key as FieldInfo;
				f.SetValue(this, entry.Value);
			}
			values = null;
		}
		else
		{
			// Make sure it's the last component
			var components = GetComponents<Component>();

			// But first make sure there aren't any other SteamVR_Cameras on this object.
			for (int i = 0; i < components.Length; i++)
			{
				var c = components[i] as SteamVR_Camera;
				if (c != null && c != this)
				{
					if (c.flip != null)
						Object.DestroyImmediate(c.flip);
					Object.DestroyImmediate(c);
				}
			}

			components = GetComponents<Component>();

			if (this != components[components.Length - 1] || flip == null)
			{
				var go = gameObject;
				if (flip == null)
					flip = go.AddComponent<SteamVR_CameraFlip>();

				// Store off values to be restored on new instance
				values = new Hashtable();
				var fields = GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				foreach (var f in fields)
					if (f.IsPublic || f.IsDefined(typeof(SerializeField), true))
						values[f] = f.GetValue(this);

				GameObject.DestroyImmediate(this);
				go.AddComponent<SteamVR_Camera>().ForceLast();
			}
		}
	}

	#endregion

	#region Expand / Collapse object hierarchy

#if UNITY_EDITOR
	public bool isExpanded { get { return head != null && transform.parent == head; } }
#endif
	const string eyeSuffix = " (eye)";
	const string headSuffix = " (head)";
	const string originSuffix = " (origin)";
	public string baseName { get { return name.EndsWith(eyeSuffix) ? name.Substring(0, name.Length - eyeSuffix.Length) : name; } }
	static readonly System.Type[] headTypes = { typeof(AudioListener), typeof(GUILayer), typeof(FlareLayer) };

	// Object hierarchy creation to make it easy to parent other objects appropriately,
	// otherwise this gets called on demand at runtime. Remaining initialization is
	// performed at startup, once the hmd has been identified.
	public void Expand()
	{
		var _origin = transform.parent;
		if (_origin == null)
		{
			_origin = new GameObject(name + originSuffix).transform;
			_origin.localPosition = transform.localPosition;
			_origin.localRotation = transform.localRotation;
			_origin.localScale = transform.localScale;
		}

		if (head == null)
		{
			_head = new GameObject(name + headSuffix, typeof(SteamVR_GameView), typeof(SteamVR_TrackedObject)).transform;
			head.parent = _origin;
			head.position = transform.position;
			head.rotation = transform.rotation;
			head.localScale = Vector3.one;
			head.tag = tag;

			var camera = head.GetComponent<Camera>();
			camera.clearFlags = CameraClearFlags.Nothing;
			camera.cullingMask = 0;
			camera.eventMask = 0;
			camera.orthographic = true;
			camera.orthographicSize = 1;
			camera.nearClipPlane = 0;
			camera.farClipPlane = 1;
			camera.useOcclusionCulling = false;
		}

		if (!name.EndsWith(eyeSuffix))
			name += eyeSuffix;

		if (transform.parent != head)
		{
			transform.parent = head;
			transform.localPosition = Vector3.zero;
			transform.localRotation = Quaternion.identity;
			transform.localScale = Vector3.one;

			while (transform.childCount > 0)
				transform.GetChild(0).parent = head;

			foreach (var type in headTypes)
			{
				var component = GetComponent(type);
				if (component != null)
				{
					Object.DestroyImmediate(component);
					head.gameObject.AddComponent(type);
				}
			}
		}
	}

	public void Collapse()
	{
		transform.parent = null;

		// Move children and components from head back to camera.
		while (head.childCount > 0)
			head.GetChild(0).parent = transform;

		foreach (var type in headTypes)
		{
			var component = head.GetComponent(type);
			if (component != null)
			{
				Object.DestroyImmediate(component);
				gameObject.AddComponent(type);
			}
		}

		if (origin != null)
		{
			// If we created the origin originally, destroy it now.
			if (origin.name.EndsWith(originSuffix))
			{
				// Reparent any children so we don't accidentally delete them.
				var _origin = origin;
				while (_origin.childCount > 0)
					_origin.GetChild(0).parent = _origin.parent;

				Object.DestroyImmediate(_origin.gameObject);
			}
			else
			{
				transform.parent = origin;
			}
		}

		Object.DestroyImmediate(head.gameObject);
		_head = null;

		if (name.EndsWith(eyeSuffix))
			name = name.Substring(0, name.Length - eyeSuffix.Length);
	}

	#endregion

	#region Render callbacks

	void OnPreRender()
	{
		if (flip)
			flip.enabled = (SteamVR_Render.Top() == this);

		var headCam = head.GetComponent<Camera>();
		if (headCam != null)
			headCam.enabled = (SteamVR_Render.Top() == this);

		if (wireframe)
			GL.wireframe = true;
	}

	void OnPostRender()
	{
		if (wireframe)
			GL.wireframe = false;
	}

	void OnRenderImage(RenderTexture src, RenderTexture dest)
	{
		if (SteamVR_Render.Top() == this)
		{
			var vr = SteamVR.instance;
			var i = (int)SteamVR_Render.eye;
			var bounds = vr.textureBounds[i];
			vr.compositor.Submit(SteamVR_Render.eye, vr.graphicsAPI, src.GetNativeTexturePtr(), ref bounds);
		}

		Graphics.SetRenderTarget(dest);
		SteamVR_Camera.blitMaterial.mainTexture = src;

		GL.PushMatrix();
		GL.LoadOrtho();
		SteamVR_Camera.blitMaterial.SetPass(0);
		GL.Begin(GL.QUADS);
		GL.TexCoord2(0.0f, 0.0f); GL.Vertex3(-1,  1, 0);
		GL.TexCoord2(1.0f, 0.0f); GL.Vertex3( 1,  1, 0);
		GL.TexCoord2(1.0f, 1.0f); GL.Vertex3( 1, -1, 0);
		GL.TexCoord2(0.0f, 1.0f); GL.Vertex3(-1, -1, 0);
		GL.End();
		GL.PopMatrix();
	}

	#endregion
}

