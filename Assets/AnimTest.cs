using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimTest : MonoBehaviour
{
    public Animator target;

    public string stateName;
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            target.Play(stateName);
        }
    }
}
