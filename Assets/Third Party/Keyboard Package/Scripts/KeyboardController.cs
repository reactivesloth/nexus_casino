using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum KeyboardType
{
    Full,
    OnlyLetters,
    OnlyNumbers
}

public class KeyboardController : MonoBehaviour
{
    [SerializeField] GameObject smallAlphaRow1;
    [SerializeField] GameObject smallAlphaRow2;
    [SerializeField] GameObject smallAlphaRow3;

    [SerializeField] GameObject capitalAlphaRow1;
    [SerializeField] GameObject capitalAlphaRow2;
    [SerializeField] GameObject capitalAlphaRow3;

    [SerializeField] GameObject numbers;
    [SerializeField] GameObject splCharsNum1;
    [SerializeField] GameObject splCharsNum2;
    [SerializeField] GameObject splChars1;
    [SerializeField] GameObject splChars2;

    [SerializeField] GameObject actionNumbers;
    [SerializeField] GameObject actionCapitalLetters;
    [SerializeField] GameObject actionSmallLetters;
    [SerializeField] GameObject actionEnter;
    [SerializeField] GameObject actionSpace;
    [SerializeField] GameObject actionDelete;
    
    private bool isSmallLettersShown = true;

    public void ShowCapitalLetters() {
        isSmallLettersShown = false;

        actionNumbers.SetActive(true);
        actionSmallLetters.SetActive(false);
        actionCapitalLetters.SetActive(false);

        smallAlphaRow1.SetActive(false);
        smallAlphaRow2.SetActive(false);
        smallAlphaRow3.SetActive(false);

        capitalAlphaRow1.SetActive(true);
        capitalAlphaRow2.SetActive(true);
        capitalAlphaRow3.SetActive(true);

        numbers.SetActive(false);
        splCharsNum1.SetActive(false);
        splCharsNum2.SetActive(false);
        splChars1.SetActive(false);
        splChars2.SetActive(false);
        actionEnter.SetActive(true);
        actionSpace.SetActive(true);
        actionDelete.SetActive(false);
    }
    
    public void ShowSmallLetters() {
        isSmallLettersShown = true;

        actionNumbers.SetActive(true);
        actionSmallLetters.SetActive(false);
        actionCapitalLetters.SetActive(false);

        capitalAlphaRow1.SetActive(false);
        capitalAlphaRow2.SetActive(false);
        capitalAlphaRow3.SetActive(false);

        smallAlphaRow1.SetActive(true);
        smallAlphaRow2.SetActive(true);
        smallAlphaRow3.SetActive(true);

        numbers.SetActive(false);
        splCharsNum1.SetActive(false);
        splCharsNum2.SetActive(false);
        splChars1.SetActive(false);
        splChars2.SetActive(false);
        actionEnter.SetActive(true);
        actionSpace.SetActive(true);
        actionDelete.SetActive(false);
    }

    public void ShowSpecialCharsNum() {
        actionNumbers.SetActive(false);

        if(isSmallLettersShown) {
            actionSmallLetters.SetActive(true);
            actionCapitalLetters.SetActive(false);
        } else {
            actionSmallLetters.SetActive(false);
            actionCapitalLetters.SetActive(true);
        }

        smallAlphaRow1.SetActive(false);
        smallAlphaRow2.SetActive(false);
        smallAlphaRow3.SetActive(false);

        capitalAlphaRow1.SetActive(false);
        capitalAlphaRow2.SetActive(false);
        capitalAlphaRow3.SetActive(false);

        numbers.SetActive(true);
        splCharsNum1.SetActive(true);
        splCharsNum2.SetActive(true);

        splChars1.SetActive(false);
        splChars2.SetActive(false);
        actionEnter.SetActive(true);
        actionSpace.SetActive(true);
        actionDelete.SetActive(false);
    }

    public void ShowSpecialChars() {
        actionNumbers.SetActive(false);

        if(isSmallLettersShown) {
            actionSmallLetters.SetActive(true);
            actionCapitalLetters.SetActive(false);
        } else {
            actionSmallLetters.SetActive(false);
            actionCapitalLetters.SetActive(true);
        }

        smallAlphaRow1.SetActive(false);
        smallAlphaRow2.SetActive(false);
        smallAlphaRow3.SetActive(false);

        capitalAlphaRow1.SetActive(false);
        capitalAlphaRow2.SetActive(false);
        capitalAlphaRow3.SetActive(false);

        numbers.SetActive(true);
        splCharsNum1.SetActive(false);
        splCharsNum2.SetActive(false);

        splChars1.SetActive(true);
        splChars2.SetActive(true);
        actionEnter.SetActive(true);
        actionSpace.SetActive(true);
        actionDelete.SetActive(false);
    }

    public void SetType(KeyboardType type)
    {
        switch (type)
        {
            case KeyboardType.Full:
                ShowSmallLetters();
                break;
            case KeyboardType.OnlyLetters:
                actionNumbers.SetActive(false);
                actionSmallLetters.SetActive(false);
                actionCapitalLetters.SetActive(false);
                smallAlphaRow1.SetActive(true);
                smallAlphaRow2.SetActive(true);
                smallAlphaRow3.SetActive(true);
                capitalAlphaRow1.SetActive(false);
                capitalAlphaRow2.SetActive(false);
                capitalAlphaRow3.SetActive(false);
                numbers.SetActive(false);
                splCharsNum1.SetActive(false);
                splCharsNum2.SetActive(false);
                splChars1.SetActive(false);
                splChars2.SetActive(false);
                actionEnter.SetActive(false);
                actionSpace.SetActive(false);
                actionDelete.SetActive(false);
                break;
            case KeyboardType.OnlyNumbers:
                actionNumbers.SetActive(false);
                actionSmallLetters.SetActive(false);
                actionCapitalLetters.SetActive(false);
                smallAlphaRow1.SetActive(false);
                smallAlphaRow2.SetActive(false);
                smallAlphaRow3.SetActive(false);
                capitalAlphaRow1.SetActive(false);
                capitalAlphaRow2.SetActive(false);
                capitalAlphaRow3.SetActive(false);
                numbers.SetActive(true);
                splCharsNum1.SetActive(false);
                splCharsNum2.SetActive(false);
                splChars1.SetActive(false);
                splChars2.SetActive(false);
                actionEnter.SetActive(false);
                actionSpace.SetActive(false);
                actionDelete.SetActive(true);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }
}
