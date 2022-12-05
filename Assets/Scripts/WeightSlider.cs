using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WeightSlider : MonoBehaviour
{

    public TMP_Text titleText;
    public TMP_Text valueText;
    public Slider slider;
    public string title;
    public CanvasGroup canvasGroup;
    public Button deleteButton;
    public UiElementEvents titleButton;
    public void Init(string title, float f, Action<float> OnSliderChanged, Action OnDeletePressed, Action OnTitlePressed, Action OnTitleReleased)
    {
        slider.value = f;
        this.title = title;
        titleText.text = title;
        slider.onValueChanged.AddListener(OnSliderChanged.Invoke);
        slider.onValueChanged.AddListener((f) =>
        {
            UpdateValue();
        });
        deleteButton.onClick.AddListener(() => OnDeletePressed?.Invoke());
        titleButton.OnPressed += OnTitlePressed;
        titleButton.OnReleased += OnTitleReleased;
    }

    public void Set(bool isKey, float key)
    {
        canvasGroup.alpha = isKey ? 1f : 0.3f;
        slider.SetValueWithoutNotify(key);
        UpdateValue();
    }
    
    public void Set(bool isKey)
    {
        canvasGroup.alpha = isKey ? 1f : 0.3f;
    }

    public void UpdateValue()
    {
        valueText.text = "" + slider.value.ToString("0.0");
    }
}
