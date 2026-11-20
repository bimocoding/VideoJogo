using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class SetupStaminaBar
{
    [MenuItem("Combat/Setup Stamina Bar (Green) & Health (Red)")]
    public static void Execute()
    {
        var hud = Object.FindAnyObjectByType<PlayerHealthHUD>();
        if (hud == null)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "SampleScene")
            {
                scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
                hud = Object.FindAnyObjectByType<PlayerHealthHUD>();
            }
        }

        if (hud == null)
        {
            Debug.LogWarning("[SetupStaminaBar] No se encontró PlayerHealthHUD en la escena.");
            return;
        }

        // 1. Configurar Salud a ROJO
        hud.normalHealthColor = new Color(0.92f, 0.18f, 0.18f, 1f); // Rojo vivo
        hud.lowHealthColor = new Color(0.65f, 0.08f, 0.08f, 1f);   // Rojo crítico
        hud.catchupColor = new Color(0.95f, 0.45f, 0.20f, 0.90f);  // Naranja-rojo chip
        if (hud.healthFillImage != null)
        {
            hud.healthFillImage.color = hud.normalHealthColor;
        }

        // 2. Configurar Stamina a VERDE
        hud.normalStaminaColor = new Color(0.18f, 0.88f, 0.38f, 1f); // Verde esmeralda vivo
        hud.lowStaminaColor = new Color(0.12f, 0.50f, 0.22f, 1f);    // Verde oscuro
        hud.staminaCatchupColor = new Color(0.40f, 0.95f, 0.55f, 0.85f); // Verde chip damage
        hud.insufficientStaminaFlashColor = new Color(1f, 0.3f, 0.3f, 1f);

        // 3. Reajustar contenedor para albergar ambas barras
        if (hud.healthBarContainer != null)
        {
            hud.healthBarContainer.sizeDelta = new Vector2(340f, 78f);

            var healthFrame = hud.healthBarContainer.Find("HealthBar_Frame") as RectTransform;
            if (healthFrame != null)
            {
                healthFrame.anchoredPosition = new Vector2(0f, -22f);
                healthFrame.sizeDelta = new Vector2(330f, 20f);
            }

            // 4. Crear o configurar StaminaBar_Frame
            Transform existingStamina = hud.healthBarContainer.Find("StaminaBar_Frame");
            GameObject staminaFrameGo;
            if (existingStamina != null)
            {
                staminaFrameGo = existingStamina.gameObject;
            }
            else if (healthFrame != null)
            {
                staminaFrameGo = Object.Instantiate(healthFrame.gameObject, hud.healthBarContainer);
                staminaFrameGo.name = "StaminaBar_Frame";
            }
            else
            {
                Debug.LogError("[SetupStaminaBar] No se encontró HealthBar_Frame para clonar.");
                return;
            }

            var staminaFrameRect = staminaFrameGo.GetComponent<RectTransform>();
            staminaFrameRect.anchoredPosition = new Vector2(0f, -46f);
            staminaFrameRect.sizeDelta = new Vector2(330f, 18f);

            var frameImg = staminaFrameGo.GetComponent<Image>();
            if (frameImg != null)
            {
                frameImg.color = new Color(0.06f, 0.10f, 0.08f, 1.0f);
            }

            Transform bgTrans = staminaFrameGo.transform.Find("StaminaBar_Background");
            if (bgTrans == null) bgTrans = staminaFrameGo.transform.Find("HealthBar_Background");
            if (bgTrans != null)
            {
                bgTrans.name = "StaminaBar_Background";
                var bgImg = bgTrans.GetComponent<Image>();
                if (bgImg != null) bgImg.color = new Color(0.08f, 0.16f, 0.10f, 1.0f);

                Transform catchupTrans = bgTrans.Find("StaminaBar_Catchup");
                if (catchupTrans == null) catchupTrans = bgTrans.Find("HealthBar_Catchup");
                if (catchupTrans != null)
                {
                    catchupTrans.name = "StaminaBar_Catchup";
                    var catchupImg = catchupTrans.GetComponent<Image>();
                    if (catchupImg != null)
                    {
                        catchupImg.color = hud.staminaCatchupColor;
                        hud.staminaCatchupImage = catchupImg;
                    }
                }

                Transform fillTrans = bgTrans.Find("StaminaBar_Fill");
                if (fillTrans == null) fillTrans = bgTrans.Find("HealthBar_Fill");
                if (fillTrans != null)
                {
                    fillTrans.name = "StaminaBar_Fill";
                    var fillImg = fillTrans.GetComponent<Image>();
                    if (fillImg != null)
                    {
                        fillImg.color = hud.normalStaminaColor;
                        hud.staminaFillImage = fillImg;
                    }
                }

                Transform textTrans = bgTrans.Find("StaminaNumbers_Text");
                if (textTrans == null) textTrans = bgTrans.Find("HealthNumbers_Text");
                if (textTrans != null)
                {
                    textTrans.name = "StaminaNumbers_Text";
                    var tmp = textTrans.GetComponent<TextMeshProUGUI>();
                    if (tmp != null)
                    {
                        tmp.text = "100 / 100";
                        hud.staminaNumbersText = tmp;
                    }
                }
            }

            hud.staminaBarContainer = staminaFrameRect;
        }

        EditorUtility.SetDirty(hud);
        EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
        EditorSceneManager.SaveScene(hud.gameObject.scene);
        AssetDatabase.SaveAssets();

        Debug.Log("<color=green>[SetupStaminaBar]</color> ¡Barra de Stamina (Verde) y Barra de Salud (Roja) configuradas con éxito!");
    }
}
