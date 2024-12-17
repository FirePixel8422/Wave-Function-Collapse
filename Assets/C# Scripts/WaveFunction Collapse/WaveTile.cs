using Unity.Mathematics;
using UnityEngine;



public class WaveTile : MonoBehaviour
{
    [Header("Order: \nLeft, Right \nUp, Down \nFront, Back ")]
    public int[] connectors = new int[] { -1, -1, -1, -1, -1, -1 };
    public int[] DEBUG_connectors;

    public float weight;

    public bool3 flippable;

    public GameObject[] DEBUG_tileOptions;


    private void Start()
    {

#if UNITY_EDITOR
        //DebugTileData();
#endif
    }



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

            spawnedTextMesh.text = connectors[i].ToString();
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



    private void OnValidate()
    {
        if (connectors.Length != 6)
        {
            int[] prevConnectors = connectors;

            connectors = new int[6];

            for (int i = 0; i < prevConnectors.Length; i++)
            {
                connectors[i] = prevConnectors[i];
            }

            Debug.LogWarning("There must be 6 connectors!!!");
        }
    }
#endif
}