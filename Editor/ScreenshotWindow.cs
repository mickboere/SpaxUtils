// Put this file inside an Editor folder.
// Example: Assets/SpaxUtils/Editor/ScreenshotWindow.cs

using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SpaxUtils
{
	public class ScreenshotWindow : EditorWindow
	{
		private const string PREF_PREFIX = "ScreenshotWindow.";
		private const string PREF_SCALE = PREF_PREFIX + "Scale";
		private const string PREF_PATH = PREF_PREFIX + "Path";
		private const string PREF_OPEN_LAST_AFTER_SAVE = PREF_PREFIX + "OpenLastAfterSave";
		private const string PREF_TAB_INDEX = PREF_PREFIX + "TabIndex";

		private static readonly string[] Tabs = { "Settings", "Preview" };

		private int tabIndex;
		private int scale;
		private string saveFolderPath;
		private bool openLastAfterSave;

		private Texture2D previewTexture;
		private string lastSavedFilePath;
		private bool previewDirty = true;
		private bool captureInProgress;

		[MenuItem("Tools/Screenshot Window")]
		public static void OpenWindow()
		{
			ScreenshotWindow window = GetWindow<ScreenshotWindow>("Screenshot");
			window.minSize = new Vector2(560f, 560f);
			window.Show();
		}

		private void OnEnable()
		{
			LoadPrefs();

			if (string.IsNullOrWhiteSpace(saveFolderPath))
			{
				saveFolderPath = GetDefaultSaveFolder();
			}

			previewDirty = true;
		}

		private void OnDisable()
		{
			SavePrefs();
			DestroyPreviewTexture();
		}

		private void OnGUI()
		{
			DrawToolbar();

			EditorGUILayout.Space(6f);

			if (tabIndex == 0)
			{
				DrawSettingsTab();
			}
			else
			{
				DrawPreviewTab();
			}
		}

		private void DrawToolbar()
		{
			EditorGUI.BeginChangeCheck();
			tabIndex = GUILayout.Toolbar(tabIndex, Tabs);
			if (EditorGUI.EndChangeCheck())
			{
				SavePrefs();
			}
		}

		private void DrawSettingsTab()
		{
			EditorGUILayout.LabelField("Capture", EditorStyles.boldLabel);

			EditorGUILayout.HelpBox(
				"This tool captures the final Game View frame. " +
				"Set your framing and base resolution in the Game View itself, then use Scale for true higher-resolution output.",
				MessageType.Info);

			EditorGUILayout.HelpBox(
				"Works in both Edit Mode and Play Mode, but the Game View should be visible and focused when capturing.",
				MessageType.None);

			EditorGUILayout.Space(6f);

			EditorGUILayout.LabelField("Resolution", EditorStyles.boldLabel);

			EditorGUI.BeginChangeCheck();
			scale = EditorGUILayout.IntSlider("Scale", Mathf.Max(1, scale), 1, 8);
			if (EditorGUI.EndChangeCheck())
			{
				scale = Mathf.Max(1, scale);
				previewDirty = true;
				SavePrefs();
			}

			Vector2Int estimatedFinalResolution = GetEstimatedFinalResolution();

			EditorGUILayout.HelpBox(
				"Final output size = current Game View resolution multiplied by Scale.\n" +
				"Estimated Final Resolution: " + estimatedFinalResolution.x + " x " + estimatedFinalResolution.y,
				MessageType.None);

			EditorGUILayout.Space(6f);

			EditorGUILayout.LabelField("Saving", EditorStyles.boldLabel);

			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUI.BeginChangeCheck();
				saveFolderPath = EditorGUILayout.TextField("Save Folder", saveFolderPath);
				if (EditorGUI.EndChangeCheck())
				{
					SavePrefs();
				}

				if (GUILayout.Button("Browse", GUILayout.Width(80f)))
				{
					string selectedPath = EditorUtility.OpenFolderPanel("Select Screenshot Folder", GetSafeExistingFolder(saveFolderPath), "");
					if (!string.IsNullOrWhiteSpace(selectedPath))
					{
						saveFolderPath = selectedPath;
						SavePrefs();
					}
				}
			}

			EditorGUI.BeginChangeCheck();
			openLastAfterSave = EditorGUILayout.Toggle("Open Last", openLastAfterSave);
			if (EditorGUI.EndChangeCheck())
			{
				SavePrefs();
			}

			EditorGUILayout.Space(12f);

			DrawActionButtons();
		}

		private void DrawPreviewTab()
		{
			if (previewTexture == null || previewDirty)
			{
				EditorGUILayout.HelpBox("Preview is not up to date.", MessageType.Info);
			}

			using (new EditorGUILayout.HorizontalScope())
			{
				GUI.enabled = !captureInProgress;
				if (GUILayout.Button("Refresh Preview", GUILayout.Height(24f)))
				{
					RequestCapture(false, true);
				}
				GUI.enabled = true;

				if (GUILayout.Button("Clear Preview", GUILayout.Height(24f)))
				{
					DestroyPreviewTexture();
					previewDirty = true;
				}
			}

			EditorGUILayout.Space(8f);

			DrawPreviewArea();

			EditorGUILayout.Space(8f);

			Vector2Int estimatedFinalResolution = GetEstimatedFinalResolution();
			EditorGUILayout.HelpBox(
				"Estimated Final Resolution: " + estimatedFinalResolution.x + " x " + estimatedFinalResolution.y,
				MessageType.None);

			EditorGUILayout.Space(12f);

			DrawActionButtons();
		}

		private void DrawPreviewArea()
		{
			Rect outerRect = GUILayoutUtility.GetRect(10f, 10000f, 200f, 10000f);

			GUI.Box(outerRect, GUIContent.none);

			if (previewTexture == null)
			{
				GUI.Label(
					new Rect(outerRect.x + 12f, outerRect.y + 12f, outerRect.width - 24f, 40f),
					"No preview available.\nClick Refresh Preview.",
					EditorStyles.centeredGreyMiniLabel);
				return;
			}

			float padding = 10f;
			Rect contentRect = new Rect(
				outerRect.x + padding,
				outerRect.y + padding,
				outerRect.width - padding * 2f,
				outerRect.height - padding * 2f);

			float texWidth = previewTexture.width;
			float texHeight = previewTexture.height;

			if (texWidth <= 0f || texHeight <= 0f)
			{
				return;
			}

			float aspect = texWidth / texHeight;
			float drawWidth = contentRect.width;
			float drawHeight = drawWidth / aspect;

			if (drawHeight > contentRect.height)
			{
				drawHeight = contentRect.height;
				drawWidth = drawHeight * aspect;
			}

			Rect drawRect = new Rect(
				contentRect.x + (contentRect.width - drawWidth) * 0.5f,
				contentRect.y + (contentRect.height - drawHeight) * 0.5f,
				drawWidth,
				drawHeight);

			EditorGUI.DrawPreviewTexture(drawRect, previewTexture, null, ScaleMode.ScaleToFit);

			Rect infoRect = new Rect(contentRect.x, contentRect.y, contentRect.width, 20f);
			GUI.Label(
				infoRect,
				"Preview: " + previewTexture.width + " x " + previewTexture.height,
				EditorStyles.whiteMiniLabel);
		}

		private void DrawActionButtons()
		{
			GUI.enabled = !captureInProgress;

			if (GUILayout.Button(captureInProgress ? "Capturing..." : "Take Screenshot", GUILayout.Height(42f)))
			{
				RequestCapture(true, true);
			}

			GUI.enabled = true;

			using (new EditorGUILayout.HorizontalScope())
			{
				GUI.enabled = !captureInProgress;
				if (GUILayout.Button("Open Last", GUILayout.Height(28f)))
				{
					OpenLastFile();
				}
				GUI.enabled = true;

				if (GUILayout.Button("Open Folder", GUILayout.Height(28f)))
				{
					OpenFolder();
				}

				bool newOpenLastAfterSave = GUILayout.Toggle(openLastAfterSave, "Open Last", "Button", GUILayout.Height(28f), GUILayout.Width(100f));
				if (newOpenLastAfterSave != openLastAfterSave)
				{
					openLastAfterSave = newOpenLastAfterSave;
					SavePrefs();
				}
			}

			if (!string.IsNullOrWhiteSpace(lastSavedFilePath))
			{
				EditorGUILayout.HelpBox("Last: " + lastSavedFilePath, MessageType.None);
			}
		}

		private void RequestCapture(bool saveToDisk, bool updatePreview)
		{
			if (captureInProgress)
			{
				return;
			}

			ScreenshotCaptureRunner runner = ScreenshotCaptureRunner.GetOrCreate();
			if (runner == null)
			{
				EditorUtility.DisplayDialog("Screenshot", "Could not create capture runner.", "OK");
				return;
			}

			captureInProgress = true;
			Repaint();

			EditorApplication.QueuePlayerLoopUpdate();
			SceneView.RepaintAll();

			runner.BeginCapture(
				scale,
				OnCaptureComplete,
				OnCaptureFailed,
				saveToDisk,
				updatePreview
			);
		}

		private void OnCaptureComplete(Texture2D capturedTexture, bool saveToDisk, bool updatePreview)
		{
			captureInProgress = false;

			if (capturedTexture == null)
			{
				previewDirty = true;
				Repaint();
				return;
			}

			if (updatePreview)
			{
				SetPreviewTexture(capturedTexture);
				previewDirty = false;
			}

			if (saveToDisk)
			{
				try
				{
					string folder = string.IsNullOrWhiteSpace(saveFolderPath) ? GetDefaultSaveFolder() : saveFolderPath;
					EnsureFolderExists(folder);

					string filePath = BuildOutputFilePath(folder);
					byte[] pngBytes = ImageConversion.EncodeToPNG(capturedTexture);
					File.WriteAllBytes(filePath, pngBytes);
					lastSavedFilePath = filePath;

					AssetDatabase.Refresh();

					if (openLastAfterSave)
					{
						OpenFileWithDefaultApp(filePath);
					}
				}
				catch (Exception ex)
				{
					UnityEngine.Debug.LogError("Screenshot save failed: " + ex);
					EditorUtility.DisplayDialog("Screenshot", "Saving screenshot failed.\n\n" + ex.Message, "OK");
				}
			}

			DestroyImmediate(capturedTexture);

			Repaint();
		}

		private void OnCaptureFailed(string error)
		{
			captureInProgress = false;
			UnityEngine.Debug.LogError(error);
			EditorUtility.DisplayDialog("Screenshot", error, "OK");
			Repaint();
		}

		private void SetPreviewTexture(Texture2D source)
		{
			DestroyPreviewTexture();

			if (source == null)
			{
				previewTexture = null;
				return;
			}

			previewTexture = DuplicateTexture(source);
		}

		private Texture2D DuplicateTexture(Texture2D source)
		{
			if (source == null)
			{
				return null;
			}

			Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false);
			copy.name = source.name + "_Copy";
			copy.SetPixels(source.GetPixels());
			copy.Apply(false, false);
			return copy;
		}

		private void DestroyPreviewTexture()
		{
			if (previewTexture != null)
			{
				DestroyImmediate(previewTexture);
				previewTexture = null;
			}
		}

		private Vector2Int GetEstimatedFinalResolution()
		{
			int finalWidth = Mathf.Max(1, Screen.width) * Mathf.Max(1, scale);
			int finalHeight = Mathf.Max(1, Screen.height) * Mathf.Max(1, scale);
			return new Vector2Int(finalWidth, finalHeight);
		}

		private string BuildOutputFilePath(string folder)
		{
			string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
			string fileName = "Screenshot_" + timestamp + ".png";
			return Path.Combine(folder, fileName);
		}

		private void OpenLastFile()
		{
			if (string.IsNullOrWhiteSpace(lastSavedFilePath) || !File.Exists(lastSavedFilePath))
			{
				EditorUtility.DisplayDialog("Screenshot", "No saved screenshot found.", "OK");
				return;
			}

			OpenFileWithDefaultApp(lastSavedFilePath);
		}

		private void OpenFolder()
		{
			string folder = string.IsNullOrWhiteSpace(saveFolderPath) ? GetDefaultSaveFolder() : saveFolderPath;
			EnsureFolderExists(folder);
			EditorUtility.RevealInFinder(folder);
		}

		private void OpenFileWithDefaultApp(string filePath)
		{
			try
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = filePath,
					UseShellExecute = true
				});
			}
			catch (Exception ex)
			{
				UnityEngine.Debug.LogError("Failed to open file: " + ex);
				EditorUtility.DisplayDialog("Screenshot", "Failed to open screenshot.\n\n" + ex.Message, "OK");
			}
		}

		private void EnsureFolderExists(string folder)
		{
			if (!Directory.Exists(folder))
			{
				Directory.CreateDirectory(folder);
			}
		}

		private string GetDefaultSaveFolder()
		{
			return Path.GetFullPath(Path.Combine(Application.dataPath, "../Screenshots"));
		}

		private string GetSafeExistingFolder(string path)
		{
			if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
			{
				return path;
			}

			return GetDefaultSaveFolder();
		}

		private void LoadPrefs()
		{
			tabIndex = EditorPrefs.GetInt(PREF_TAB_INDEX, 0);
			scale = EditorPrefs.GetInt(PREF_SCALE, 1);
			saveFolderPath = EditorPrefs.GetString(PREF_PATH, GetDefaultSaveFolder());
			openLastAfterSave = EditorPrefs.GetBool(PREF_OPEN_LAST_AFTER_SAVE, false);

			scale = Mathf.Max(1, scale);
			tabIndex = Mathf.Clamp(tabIndex, 0, Tabs.Length - 1);
		}

		private void SavePrefs()
		{
			EditorPrefs.SetInt(PREF_TAB_INDEX, tabIndex);
			EditorPrefs.SetInt(PREF_SCALE, Mathf.Max(1, scale));
			EditorPrefs.SetString(PREF_PATH, saveFolderPath ?? string.Empty);
			EditorPrefs.SetBool(PREF_OPEN_LAST_AFTER_SAVE, openLastAfterSave);
		}

		[ExecuteAlways]
		private class ScreenshotCaptureRunner : MonoBehaviour
		{
			private static ScreenshotCaptureRunner instance;

			private Action<Texture2D, bool, bool> onComplete;
			private Action<string> onFailed;
			private bool saveToDisk;
			private bool updatePreview;
			private int superSize;
			private bool busy;

			public static ScreenshotCaptureRunner GetOrCreate()
			{
				if (instance != null)
				{
					return instance;
				}

				ScreenshotCaptureRunner existing = FindFirstObjectByType<ScreenshotCaptureRunner>();
				if (existing != null)
				{
					instance = existing;
					return instance;
				}

				GameObject go = new GameObject("ScreenshotCaptureRunner");
				go.hideFlags = HideFlags.HideAndDontSave;
				instance = go.AddComponent<ScreenshotCaptureRunner>();
				return instance;
			}

			public void BeginCapture(
				int captureSuperSize,
				Action<Texture2D, bool, bool> completeCallback,
				Action<string> failedCallback,
				bool shouldSaveToDisk,
				bool shouldUpdatePreview)
			{
				if (busy)
				{
					failedCallback?.Invoke("A screenshot capture is already in progress.");
					return;
				}

				superSize = Mathf.Max(1, captureSuperSize);
				onComplete = completeCallback;
				onFailed = failedCallback;
				saveToDisk = shouldSaveToDisk;
				updatePreview = shouldUpdatePreview;
				busy = true;

				StartCoroutine(CaptureRoutine());
			}

			private IEnumerator CaptureRoutine()
			{
				yield return null;
				yield return new WaitForEndOfFrame();

				Texture2D captured = null;

				try
				{
					captured = ScreenCapture.CaptureScreenshotAsTexture(superSize);
				}
				catch (Exception ex)
				{
					busy = false;
					onFailed?.Invoke(
						"Game View capture failed.\n\n" +
						ex.Message + "\n\n" +
						"Make sure the Game View is visible and focused, then try again."
					);
					yield break;
				}

				busy = false;

				if (captured == null)
				{
					onFailed?.Invoke(
						"Game View capture returned null.\n\n" +
						"Make sure the Game View is visible and focused, then try again."
					);
					yield break;
				}

				onComplete?.Invoke(captured, saveToDisk, updatePreview);
			}

			private void OnDisable()
			{
				if (instance == this)
				{
					instance = null;
				}
			}
		}
	}
}
