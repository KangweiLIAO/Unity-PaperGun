using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public bool lockCursor;
    public bool showCursor;

    // Start is called before the first frame update
    void Start()
    {
        if (lockCursor)
        {
            // Lock the cursor inside the game window
            Cursor.lockState = CursorLockMode.Confined;  // Ensures the cursor is locked within the window
        }
        Cursor.visible = showCursor;

    }

    // Update is called once per frame
    void Update()
    {

    }
}
