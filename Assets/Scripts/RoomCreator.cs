using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using UnityEngine.WSA;
using UnityEditor.TerrainTools;
using UnityEngine.UIElements;
using System;
using UnityEngine.SocialPlatforms;

public class RoomCreator : EditorWindow
{
    [MenuItem("Tools/RoomTools")]
    public static void OpernWindow() => GetWindow<RoomCreator>();

    bool ToolOn = true;

    GameObject[] Rooms;

    GameObject CurrentRoom;
    GameObject CurrentRoomPreviou;
    [SerializeField] bool[] Selected;
    GameObject folder;
    SerializedObject so;
    GameObject[] AllObjs;
    GameObject[] AllChild;
    GameObject[] ChildInObject;
    List<GameObject> DoorsInScene = new List<GameObject>();
    GameObject closestDoor;

    bool AvalableDoorup;
    bool AvalableDoordown;
    bool AvalableDoorleft;
    bool AvalableDoorright;

    float currentRoomRot = 0;
    int SnapDoorRotIndex;
    bool isInRange;
    bool isCompatible;

    Vector3 roomSnapPos;

    Quaternion rot = Quaternion.identity;

    private void OnEnable()
    {
        ToolOn = true;

        currentRoomRot = 0;

        so = new SerializedObject(this);

        SceneView.duringSceneGui += DuringGUI;

        string[] RoomsFile = AssetDatabase.FindAssets("t:prefab", new[] { "Assets/Prefab/Rooms" });
        string[] CorridorsFile = AssetDatabase.FindAssets("t:prefab", new[] { "Assets/Prefab/Rooms" });

        IEnumerable<string> paths = RoomsFile.Select(AssetDatabase.GUIDToAssetPath);
        Rooms = paths.Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToArray();


        if (Selected == null || Selected.Length != Rooms.Length)
        {
            Selected = new bool[Rooms.Length];
        }

    }


    private void OnGUI()
    {
        so.Update();
        GUIStyle style = GUI.skin.GetStyle("Label");
        style.alignment = TextAnchor.MiddleCenter;
        GUILayout.BeginArea(new Rect(10, 10, position.width - 20, 300));





        if (ToolOn)
        {
            GUILayout.Space(10);
            if (GUILayout.Button("Tool on", GUILayout.Height(40)))
            {
                ToolOn = false;
            }
        }
        else
        {
            GUILayout.Space(10);
            if (GUILayout.Button("Tool off", GUILayout.Height(40)))
            {
                ToolOn = true;
            }
        }

        if (ToolOn)
        {

            GUILayout.Space(10);
            if (GUILayout.Button("Rooms", GUILayout.Height(40)))
            {
                string[] RoomsFile = AssetDatabase.FindAssets("t:prefab", new[] { "Assets/Prefab/Rooms" });

                IEnumerable<string> paths = RoomsFile.Select(AssetDatabase.GUIDToAssetPath);
                Rooms = paths.Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToArray();
            }

            GUILayout.Space(10);
            if (GUILayout.Button("Corridors", GUILayout.Height(40)))
            {
                string[] CorridorsFile = AssetDatabase.FindAssets("t:prefab", new[] { "Assets/Prefab/Corridors" });

                IEnumerable<string> paths = CorridorsFile.Select(AssetDatabase.GUIDToAssetPath);
                Rooms = paths.Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToArray();
            }

            GUILayout.Space(10);
            if (GUILayout.Button("Undo", GUILayout.Height(40)))
            {
                Undo.PerformUndo();
            }

            GUILayout.Space(10);
            if (GUILayout.Button("Redo", GUILayout.Height(40)))
            {
                Undo.PerformRedo();
            }
        }

        GUILayout.EndArea();

    }


    private void OnDisable()
    {

        SceneView.duringSceneGui -= DuringGUI;
    }


    private void DuringGUI(SceneView sceneView)
    {
        Handles.BeginGUI();

        GUILayout.Label("Press left click to place a room or use CTRL Q and CTRL E to rotate the room");

        if (ToolOn)
        {
            if (Rooms != null)
            {
                InSceneButtons();
                ScanForExistingDoors();
                GetClosestDoor();



                if (CurrentRoom != null)
                {
                    roomPreviouInScene(sceneView);
                    checkForAalableDoorsOnPreviou();
                }


            }

            Event e = Event.current;


            if (e.type == EventType.KeyDown && e.control)
            {
                if (e.keyCode == KeyCode.Q)
                {
                    RotateRooms(90);
                }
                else if (e.keyCode == KeyCode.E)
                {
                    RotateRooms(-90);
                }

            }

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                instantiateRoom();
            }

        }
    }


    private void InSceneButtons()
    {
        Rect rect = new Rect(10, 10, 100, 100);

        for (int i = 0; i < Rooms.Length; i++)
        {
            GameObject prefab = Rooms[i];
            Texture icon = AssetPreview.GetAssetPreview(prefab);
            EditorGUI.BeginChangeCheck();
            Selected[i] = GUI.Button(rect, icon);
            if (Selected[i])
            {
                CurrentRoom = prefab;
                rot = CurrentRoom.transform.rotation;
            }
            rect.y += rect.height + 20;
        }
        Handles.EndGUI();
    }


    void roomPreviouInScene(SceneView sceneView)
    {

        if (isCompatible && isInRange)
        {
            if (CurrentRoom != null && Event.current.type == EventType.Repaint)
            {
                Vector3 cursorPosition = roomSnapPos;
                Quaternion cursorRotation = Quaternion.Euler(0f, currentRoomRot, 0f);
                Matrix4x4 matrix = Matrix4x4.TRS(cursorPosition, cursorRotation, Vector3.one);


                MeshFilter[] Mf = CurrentRoom.GetComponentsInChildren<MeshFilter>();

                foreach (MeshFilter filter in Mf)
                {
                    Matrix4x4 childToPoint = filter.transform.localToWorldMatrix;
                    Matrix4x4 childToWorldMatrix = matrix * childToPoint;

                    Mesh mesh = filter.sharedMesh;
                    Material mat = filter.GetComponent<MeshRenderer>().sharedMaterial;

                    mat.SetPass(0);

                    Graphics.DrawMesh(mesh, childToWorldMatrix, mat, 0, sceneView.camera);
                }
            }
        }
        else
        {
            if (CurrentRoom != null && Event.current.type == EventType.Repaint)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit) && hit.collider != null)
                {
                    Vector3 cursorPosition = hit.point + hit.normal * 0.5f;
                    Quaternion cursorRotation = Quaternion.Euler(0f, currentRoomRot, 0f);
                    Matrix4x4 matrix = Matrix4x4.TRS(cursorPosition, cursorRotation, Vector3.one);


                    MeshFilter[] Mf = CurrentRoom.GetComponentsInChildren<MeshFilter>();

                    foreach (MeshFilter filter in Mf)
                    {
                        Matrix4x4 childToPoint = filter.transform.localToWorldMatrix;
                        Matrix4x4 childToWorldMatrix = matrix * childToPoint;

                        Mesh mesh = filter.sharedMesh;
                        Material mat = filter.GetComponent<MeshRenderer>().sharedMaterial;

                        mat.SetPass(0);

                        Graphics.DrawMesh(mesh, childToWorldMatrix, mat, 0, sceneView.camera);
                    }
                }
            }
        }
    }


    Vector3 CurrentMousePosition()
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit) && hit.collider != null)
        {
            return hit.point + hit.normal * 0.5f;
        }
        else
        {
            return Vector3.zero;
        }
    }


    void RotateRooms(int rot)
    {
        if (rot == 90)
        {
            if (currentRoomRot != 270)
            {
                currentRoomRot += rot;
            }
            else
            {
                currentRoomRot = 0;
            }
        }
        else
        {
            if (currentRoomRot != 0)
            {
                currentRoomRot += rot;
            }
            else
            {
                currentRoomRot = 270;
            }
        }
    }


    void ScanForExistingDoors()
    {
        AllObjs = null;
        AllObjs = FindObjectsOfType<GameObject>();


        for (int i = 0; i < AllObjs.Length; i++)
        {
            if (AllObjs[i].layer == 3 && AllObjs[i] != null)
            {
                if (!DoorsInScene.Contains(AllObjs[i]))
                {
                    DoorsInScene.Add(AllObjs[i]);
                }
            }
        }

        for (int x = 0; x < DoorsInScene.Count; x++)
        {
            if (DoorsInScene[x] == null)
            {
                DoorsInScene.RemoveAt(x);
            }
        }


    }


    void GetClosestDoor()
    {
        GameObject snapDoor = null;

        float currentClosestDistance;

        foreach (var door in DoorsInScene)
        {
            if (snapDoor == null)
            {
                snapDoor = door;
            }
            else
            {
                currentClosestDistance = (CurrentMousePosition() - snapDoor.transform.position).magnitude;

                if ((CurrentMousePosition() - door.transform.position).magnitude < currentClosestDistance)
                {
                    snapDoor = door;
                }
            }

        }

        closestDoor = snapDoor;


        if (snapDoor.transform.eulerAngles.y == 0)
        {
            SnapDoorRotIndex = 4;
            roomSnapPos = closestDoor.transform.position + new Vector3(5, 0, 0);
        }


        if (snapDoor.transform.eulerAngles.y == 180)
        {
            SnapDoorRotIndex = 2;
            roomSnapPos = closestDoor.transform.position + new Vector3(-5, 0, 0);
        }

        if (snapDoor.transform.eulerAngles.y == 90)
        {
            SnapDoorRotIndex = 3;
            roomSnapPos = closestDoor.transform.position + new Vector3(0, 0, -5);
        }

        if (snapDoor.transform.eulerAngles.y == 270)
        {
            SnapDoorRotIndex = 1;
            roomSnapPos = closestDoor.transform.position + new Vector3(0, 0, 5);
        }

        if ((CurrentMousePosition() - snapDoor.transform.position).magnitude <= 2)
        {
            isInRange = true;
        }
        else
        {
            isInRange = false;
        }


        if (SnapDoorRotIndex == 4 && AvalableDoorup)
        {
            isCompatible = true;
        }
        else if (SnapDoorRotIndex == 2 && AvalableDoordown)
        {
            isCompatible = true;
        }
        else if (SnapDoorRotIndex == 1 && AvalableDoorright)
        {
            isCompatible = true;
        }
        else if (SnapDoorRotIndex == 3 && AvalableDoorleft)
        {
            isCompatible = true;
        }
        else
        {
            isCompatible = false;
        }

    }


    void checkForAalableDoorsOnPreviou()
    {

        GameObject roomsPV = CurrentRoom.transform.GetChild(0).gameObject;
        GameObject[] ChildsInPrev = new GameObject[roomsPV.transform.childCount];
        List<GameObject> doorsInPrev = new List<GameObject>();

        for (int i = 0; i < ChildsInPrev.Length; i++)
        {
            ChildsInPrev[i] = roomsPV.transform.GetChild(i).gameObject;
        }


        for (int i = 0; i < ChildsInPrev.Length; i++)
        {
            if (ChildsInPrev[i].layer == 3 && ChildsInPrev[i] != null)
            {
                if (!DoorsInScene.Contains(ChildsInPrev[i]))
                {
                    doorsInPrev.Add(ChildsInPrev[i]);
                }
            }
        }


        for (int x = 0; x < doorsInPrev.Count; x++)
        {
            if (doorsInPrev[x] == null)
            {
                doorsInPrev.RemoveAt(x);
            }
        }


        for (int i = 0; i < doorsInPrev.Count; i++)
        {
            if (i == 0)
            {
                AvalableDoorup = false;
                AvalableDoordown = false;
                AvalableDoorleft = false;
                AvalableDoorright = false;
            }

            if (currentRoomRot == 0)
            {
                if (doorsInPrev[i].transform.eulerAngles.y == 0)
                {
                    AvalableDoordown = true;
                }
                else if (doorsInPrev[i].transform.eulerAngles.y == 90)
                {
                    AvalableDoorleft = true;
                }
                else if (doorsInPrev[i].transform.eulerAngles.y == 180)
                {
                    AvalableDoorup = true;
                }
                else if (doorsInPrev[i].transform.eulerAngles.y == 270)
                {
                    AvalableDoorright = true;
                }
            }
            else if (currentRoomRot == 90)
            {
                if (doorsInPrev[i].transform.eulerAngles.y == 90)
                {
                    AvalableDoordown = true;
                }
                else if (doorsInPrev[i].transform.eulerAngles.y == 180)
                {
                    AvalableDoorleft = true;
                }
                else if (doorsInPrev[i].transform.eulerAngles.y == 270)
                {
                    AvalableDoorup = true;
                }
                else if (doorsInPrev[i].transform.eulerAngles.y == 0)
                {
                    AvalableDoorright = true;
                }
            }
            else if (currentRoomRot == 180)
            {
                if (doorsInPrev[i].transform.eulerAngles.y == 180)
                {
                    AvalableDoordown = true;
                }
                else if (doorsInPrev[i].transform.eulerAngles.y == 270)
                {
                    AvalableDoorleft = true;
                }
                else if (doorsInPrev[i].transform.eulerAngles.y == 0)
                {
                    AvalableDoorup = true;
                }
                else if (doorsInPrev[i].transform.eulerAngles.y == 90)
                {
                    AvalableDoorright = true;
                }
            }
            else if (currentRoomRot == 270)
            {
                if (doorsInPrev[i].transform.eulerAngles.y == 279)
                {
                    AvalableDoordown = true;
                }
                else if (doorsInPrev[i].transform.eulerAngles.y == 0)
                {
                    AvalableDoorleft = true;
                }
                else if (doorsInPrev[i].transform.eulerAngles.y == 90)
                {
                    AvalableDoorup = true;
                }
                else if (doorsInPrev[i].transform.eulerAngles.y == 180)
                {
                    AvalableDoorright = true;
                }
            }

        }

    }

    void instantiateRoom()
    {
        if (isInRange && isCompatible)
        {
            Vector3 spawnPosition = roomSnapPos;
            Quaternion spawnRotation = Quaternion.Euler(0, currentRoomRot, 0);

            GameObject spawnedPrefab = (GameObject)PrefabUtility.InstantiatePrefab(CurrentRoom);

            spawnedPrefab.transform.position = spawnPosition;
            spawnedPrefab.transform.rotation = spawnRotation;
            Undo.RegisterCreatedObjectUndo(spawnedPrefab, "Placed Room");
        }
    }
}


