using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

public class ChangeInstancing : MonoBehaviour
{
    public RenderPipelineAsset[] renderSettings;
    private int currentIdx = 0;

    // Start is called before the first frame update
    void Start()
    {
        SetRenderSetting(currentIdx);
    }
    private void SetRenderSetting(int idx)
    {
        GraphicsSettings.renderPipelineAsset = renderSettings[idx];
        currentIdx = idx;

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SetRenderSetting(0);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SetRenderSetting(1);
        }
    }
}
