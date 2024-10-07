using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEditor;

[CustomEditor(typeof(AutoTexture))]
public class AutoTextureEditor : Editor {
    private void OnEnable()
    {
        EditorApplication.update += UpdateTextureScale;
    }

    private void OnDisable()
    {
        EditorApplication.update -= UpdateTextureScale;
    }

    private void UpdateTextureScale()
    {
        AutoTexture autoScale = (AutoTexture)target;
        if (autoScale == null) return;

        Vector3 objectScale = autoScale.transform.localScale;
        Renderer renderer = autoScale.GetComponent<Renderer>();

        if (renderer != null && renderer.sharedMaterial != null)
        {
            renderer.sharedMaterial.mainTextureScale = new Vector2(
                objectScale.x * autoScale.textureScale.x,
                objectScale.y * autoScale.textureScale.y
            );
        }
    }
}
