// Copyright (C) 2023 ricimi. All rights reserved.
// This code can only be used under the standard Unity Asset Store EULA,
// a copy of which is available at https://unity.com/legal/as-terms.

using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Ricimi
{
	// This UI component is a specialized selection slider that allows you to
	// scroll between different text-based options.
	public class TextSelectionSlider : MonoBehaviour
	{
		public List<string> Options;
		public TextMeshProUGUI OptionText;

		public UnityEvent<int> onValueChanged;
		public int value;

		private void Start()
		{
			ChangeSelection();
		}

		public void OnPrevButtonClicked()
		{
			value--;
			if (value < 0)
			{
				value = Options.Count - 1; 
			}

			ChangeSelection();
		}

		public void OnNextButtonClicked()
		{
			value = (value + 1) % Options.Count;

			ChangeSelection();
		}

		private void ChangeSelection()
		{
			OptionText.text = Options[value];
			onValueChanged.Invoke(value);
		}

		public void AddOptions(List<string> displayOptions)
		{
			Options = displayOptions;
			ChangeSelection();
		}

		public void ClearOptions()
		{
			Options.Clear();
		}

		public void RefreshShownValue()
		{
			ChangeSelection();
		}
	}
}