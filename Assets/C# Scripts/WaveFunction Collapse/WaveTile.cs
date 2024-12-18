using System;
using Unity.Mathematics;
using UnityEngine;



public class WaveTile : MonoBehaviour
{
    [Header("Order: \nLeft, Right \nUp, Down \nFront, Back ")]
    public ConnectionTypes[] connectors = new ConnectionTypes[6] {0, 0, 0, 0, 0, 0};

    [SerializeField] private bool reset;
    [SerializeField] private bool areYouSureReset;


    public int[] DEBUG_connectors;

    public bool3 flippable;

    public GameObject[] DEBUG_tileOptions;


    private void Start()
    {

#if UNITY_EDITOR
        //DebugTileData();
#endif
    }


    [Flags]
    public enum ConnectionTypes : int
    {
        _0 = 1 << 0,  // 1
        _1 = 1 << 1,  // 2
        _2 = 1 << 2,  // 4
        _3 = 1 << 3,  // 8
        _4 = 1 << 4,  // 16
        _5 = 1 << 5,  // 32
        _6 = 1 << 6,  // 64
        _7 = 1 << 7,  // 128
        _8 = 1 << 8,  // 256
        _9 = 1 << 9,  // 512
        _10 = 1 << 10, // 1024
        _11 = 1 << 11, // 2048
        _12 = 1 << 12, // 4096
        _13 = 1 << 13, // 8192
        _14 = 1 << 14, // 16384
        _15 = 1 << 15, // 32768
        _16 = 1 << 16, // 65536
        _17 = 1 << 17, // 131072
        _18 = 1 << 18, // 262144
        _19 = 1 << 19, // 524288
        _20 = 1 << 20, // 1048576
        _21 = 1 << 21, // 2097152
        _22 = 1 << 22, // 4194304
        _23 = 1 << 23, // 8388608
        _24 = 1 << 24, // 16777216
        _25 = 1 << 25, // 33554432
        _26 = 1 << 26, // 67108864
        _27 = 1 << 27, // 134217728
        _28 = 1 << 28, // 268435456
        _29 = 1 << 29, // 536870912
        _30 = 1 << 30, // 1073741824
        _31 = 1 << 31  // 2147483648
    };



#if UNITY_EDITOR
    private void DebugTileData()
    {
        float offset = 0.215f;

        Vector3[] offsets = new Vector3[]
        {
            new Vector3(offset, 0f, 0f), // Left
            new Vector3(-offset, 0f, 0f),  // Right
            new Vector3(0f, -offset, 0f),  // Up
            new Vector3(0f, offset, 0f), // Down
            new Vector3(0f, 0f, -offset),  // Front
            new Vector3(0f, 0f, offset), // Back
        };

        TextMesh textMesh = new GameObject("text").AddComponent<TextMesh>();

        textMesh.characterSize = 0.03f;
        textMesh.fontSize = 150;
        textMesh.alignment = TextAlignment.Center;
        textMesh.anchor = TextAnchor.MiddleCenter;


        for (int i = 0; i < 6; i++)
        {
            //skip 3d
            if (i == 2 || i == 3)
            {
                continue;
            }

            TextMesh spawnedTextMesh = Instantiate(textMesh, transform.position - offsets[i], Quaternion.Euler(90, 0, 0));
            spawnedTextMesh.transform.SetParent(transform, true);

            spawnedTextMesh.text = ((int)connectors[i]).ToString();
        }

        for(int i = 0; i < 6; i++)
        {
            offset = 0.365f;

            offsets = new Vector3[]
            {
                new Vector3(offset, 0f, 0f), // Left
                new Vector3(-offset, 0f, 0f),  // Right
                new Vector3(0f, -offset, 0f),  // Up
                new Vector3(0f, offset, 0f), // Down
                new Vector3(0f, 0f, -offset),  // Front
                new Vector3(0f, 0f, offset), // Back
            };


            //skip 3d
            if (i == 2 || i == 3)
            {
                continue;
            }

            TextMesh spawnedTextMesh = Instantiate(textMesh, transform.position - offsets[i], Quaternion.Euler(90, 0, 0));
            spawnedTextMesh.transform.SetParent(transform, true);
            spawnedTextMesh.color = Color.red;

            spawnedTextMesh.text = DEBUG_connectors[i].ToString();
        }

        Destroy(textMesh.gameObject);
    }



    //force 6 connectors
    private void OnValidate()
    {
        if (reset && areYouSureReset)
        {
            reset = false;
            areYouSureReset = false;
            connectors = new ConnectionTypes[6] { 0, 0, 0, 0, 0, 0 };
        }
        areYouSureReset = false;


        if (connectors.Length != 6)
        {
            ConnectionTypes[] prev_onnectors = connectors;

            connectors = new ConnectionTypes[6];

            for (int i = 0; i < prev_onnectors.Length; i++)
            {
                connectors[i] = prev_onnectors[i];
            }

            Debug.LogWarning("There must be 6 connectors!!!");
        }
    }
#endif
}