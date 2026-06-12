using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class InspectVisuals
{
    public static string Execute()
    {
        var sb = new StringBuilder();
        var cam = Camera.main;
        if (cam != null)
        {
            sb.Append("cam=").Append(cam.name)
              .Append(" parent=").Append(cam.transform.parent != null ? cam.transform.parent.name : "none")
              .Append(" pos=").Append(cam.transform.position.ToString("F1"))
              .Append(" rot=").Append(cam.transform.eulerAngles.ToString("F0"))
              .Append(" clear=").Append(cam.clearFlags)
              .Append(" bg=").Append(cam.backgroundColor.ToString("F2"));
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            sb.Append(" postFX=").Append(data != null ? data.renderPostProcessing.ToString() : "noData");
            var comps = cam.GetComponents<Component>();
            sb.Append(" camComps=");
            foreach (var c in comps) sb.Append(c.GetType().Name).Append("|");
        }
        else sb.Append("NO MAIN CAMERA");

        sb.Append(" ;; skybox=").Append(RenderSettings.skybox != null ? RenderSettings.skybox.name : "null")
          .Append(" fog=").Append(RenderSettings.fog)
          .Append(" ambientMode=").Append(RenderSettings.ambientMode)
          .Append(" sun=").Append(RenderSettings.sun != null ? RenderSettings.sun.name : "null");

        var lights = Object.FindObjectsOfType<Light>(true);
        sb.Append(" ;; lights=");
        foreach (var l in lights)
            sb.Append(l.name).Append("(").Append(l.type).Append(",int=").Append(l.intensity)
              .Append(",shadows=").Append(l.shadows).Append(") ");

        var avatar = GameObject.Find("Wheelchair_Avatar");
        if (avatar != null)
        {
            sb.Append(";; avatar: comps=");
            foreach (var c in avatar.GetComponents<Component>()) sb.Append(c.GetType().Name).Append("|");
            sb.Append(" children=");
            foreach (Transform ch in avatar.transform) sb.Append(ch.name).Append(" ");
            var r = avatar.GetComponent<Renderer>();
            if (r != null) sb.Append(" mat=").Append(r.sharedMaterial != null ? r.sharedMaterial.name + "/" + r.sharedMaterial.shader.name : "none");
        }

        var rp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        sb.Append(" ;; urp=").Append(rp != null ? rp.name + " hdr=" + rp.supportsHDR : "none");

        var vol = Object.FindObjectOfType<Volume>(true);
        sb.Append(" volume=").Append(vol != null ? vol.name : "none");
        return sb.ToString();
    }
}
