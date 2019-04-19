﻿// Copyright (c) 2016-2017 Alexander Ong
// See LICENSE in project root for license information.

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Collections;

public class Mouse : MonoBehaviour {
    [Header("Viewing modes")]
    public Camera camera2D;
    public Camera camera3D;

    bool dragging;
    ChartEditor editor;
    GameObject selectedGameObject;
	
    public static Vector2? world2DPosition = null;
    RaycastHit[] screenToPointHits = new RaycastHit[1];

    void Start()
    {
        editor = GameObject.FindGameObjectWithTag("Editor").GetComponent<ChartEditor>();
    }

    public static bool cancel = false;
    public static RaycastResult? currentRaycastFromPointer;// = new List<RaycastResult>();
    public static GameObject currentSelectableUnderMouse;

    Vector2 initMouseDragPos = Vector2.zero;

	// Update is called once per frame
	void Update () {
        if (Globals.applicationMode != Globals.ApplicationMode.Editor)
        {
            if (selectedGameObject)
                SendOnSelectableMouseUp();

            return;
        }

        currentRaycastFromPointer = RaycastFromPointer();
        currentSelectableUnderMouse = GetSelectableObjectUnderMouse();

        Camera mainCamera = camera3D;

        Vector2 viewportPos = mainCamera.ScreenToViewportPoint(Input.mousePosition);

        if (viewportPos.x < 0 || viewportPos.x > 1 || viewportPos.y < 0 || viewportPos.y > 1)
            world2DPosition = null;
        else
        {
            Vector3 screenPos = Input.mousePosition;
            float maxY = mainCamera.WorldToScreenPoint(editor.mouseYMaxLimit.position).y;

            // Calculate world2DPosition
            if (Input.mousePosition.y > maxY)
                screenPos.y = maxY;

            Ray ray = mainCamera.ScreenPointToRay(screenPos);
            int layerMask = 1 << LayerMask.NameToLayer("Ignore Raycast");
            if (Physics.RaycastNonAlloc(ray, screenToPointHits, Mathf.Infinity, layerMask) > 0)
                world2DPosition = screenToPointHits[0].point;
            else
                world2DPosition = null;
        }

        if (cancel || (selectedGameObject && !selectedGameObject.activeSelf))
        {
            selectedGameObject = null;
            cancel = false;
        }

        // OnSelectableMouseDown
        if ((Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) && world2DPosition != null)
        {
            initMouseDragPos = (Vector2)world2DPosition;

            selectedGameObject = currentSelectableUnderMouse;

            if (selectedGameObject && selectedGameObject.activeSelf)
            {
                SelectableClick[] monos = selectedGameObject.GetComponents<SelectableClick>();

                foreach (SelectableClick mono in monos)
                {
                    mono.OnSelectableMouseDown();
                }
            }
        }  

        if ((Input.GetMouseButton(0) || Input.GetMouseButton(1)) && world2DPosition != null && world2DPosition != initMouseDragPos)
        {
            dragging = true;
        }

        // OnSelectableMouseDrag
        if (dragging && selectedGameObject && selectedGameObject.activeSelf)
        {
            SelectableClick[] monos = selectedGameObject.GetComponents<SelectableClick>();
            foreach (SelectableClick mono in monos)
            {
                mono.OnSelectableMouseDrag();
            }
        }

        // OnSelectableMouseOver
        if (currentSelectableUnderMouse && currentSelectableUnderMouse.activeSelf)
        {
            SelectableClick[] mouseOver = currentSelectableUnderMouse.GetComponents<SelectableClick>();
            foreach (SelectableClick mono in mouseOver)
            {
                mono.OnSelectableMouseOver();
            }
        }

        // OnSelectableMouseUp
        if ((Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1)) && world2DPosition != null)
        {
            SendOnSelectableMouseUp();
        }

        if (cancel || (selectedGameObject && !selectedGameObject.activeSelf))
        {
            selectedGameObject = null;
            cancel = false;
        }
    }

    void SendOnSelectableMouseUp()
    {
        if (selectedGameObject)
        {
            SelectableClick[] monos = selectedGameObject.GetComponents<SelectableClick>();
            foreach (SelectableClick mono in monos)
            {
                mono.OnSelectableMouseUp();
            }
        }

        dragging = false;

        selectedGameObject = null;
    }

    public void SwitchCamera()
    {
        if (camera2D.gameObject.activeSelf)
        {
            Set3DCamera();
        }
        else
        {
            Set2DCamera();
        }
    }

    public void Set3DCamera()
    {
        camera2D.gameObject.SetActive(false);
        camera3D.gameObject.SetActive(true);
    }

    public void Set2DCamera()
    {
        camera3D.gameObject.SetActive(false);
        camera2D.gameObject.SetActive(true);
    }

    static RaycastHit2D lowestY(RaycastHit2D[] hits)
    {
        RaycastHit2D lowestHit = hits[0];

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider.gameObject.transform.position.y < lowestHit.collider.gameObject.transform.position.y)
                lowestHit = hit;
        }

        return lowestHit;
    }

    static RaycastHit lowestY(RaycastHit[] hits)
    {
        RaycastHit lowestHit = hits[0];

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.gameObject.transform.position.y < lowestHit.collider.gameObject.transform.position.y)
                lowestHit = hit;
        }

        return lowestHit;
    }

    static void RaySortLowestY(IList<GameObject> hits)
    {
        int length = hits.Count;

        for (int i = 1; i < length; i++)
        {
            int j = i;

            while ((j > 0) && (hits[j].transform.position.y <= hits[j - 1].transform.position.y))
            {
                // Used for when comparing standard notes against open notes overlayed on top of each other when charting for drums
                if (hits[j].transform.position.y == hits[j - 1].transform.position.y)
                {
                    BoxCollider hitACol = hits[j].GetComponent<BoxCollider>();
                    BoxCollider hitBCol = hits[j - 1].GetComponent<BoxCollider>();

                    if (!(hitACol && hitBCol && hitACol.size.x < hitBCol.size.x))
                    {
                        break; 
                    }
                }

                int k = j - 1;
                GameObject temp = hits[k];
                hits[k] = hits[j];
                hits[j] = temp;

                j--;
            }
        }
    }

    static List<GameObject> hitGameObjects = new List<GameObject>();
    static RaycastHit[] hitGameObjects3d = new RaycastHit[5];
    static RaycastHit2D[] hitGameObjects2d = new RaycastHit2D[5];
    static GameObject GetSelectableObjectUnderMouse()
    {
        if (world2DPosition != null)
        {
            LayerMask mask;

            if (Globals.viewMode == Globals.ViewMode.Chart)
                mask = 1 << LayerMask.NameToLayer("ChartObject");
            else
                mask = 1 << LayerMask.NameToLayer("SongObject");

            //RaycastHit[] hits3d = Physics.RaycastAll(Camera.main.ScreenPointToRay(Input.mousePosition), Mathf.Infinity, mask);
            int hitCount = Physics.RaycastNonAlloc(Camera.main.ScreenPointToRay(Input.mousePosition), hitGameObjects3d, Mathf.Infinity, mask);

            hitGameObjects.Clear();
            if (hitCount > 0)
            {
                for (int i = 0; i < hitCount; ++i)
                    hitGameObjects.Add(hitGameObjects3d[i].collider.gameObject);

                RaySortLowestY(hitGameObjects);
                GameObject selectable = null;

                foreach (GameObject selectedObject in hitGameObjects)
                {
                    if (selectedObject.GetComponent<SelectableClick>())
                    {
                        if (selectable == null)
                            selectable = selectedObject;
                        else if (selectedObject.transform.position.y == selectable.transform.position.y && world2DPosition != null)
                        {
                            // Take the one closest to the mouse
                            float mouseX = ((Vector2)world2DPosition).x;
                            selectable = Mathf.Abs(selectedObject.transform.position.x - mouseX) < Mathf.Abs(selectable.transform.position.x - mouseX) ? selectedObject : selectable;
                        }
                        else
                            break;

                        //return selectedObject;
                    }
                }

                return selectable;
            }
            else
            {
                // Aim to hit sustain tails
                //RaycastHit2D[] hits = Physics2D.RaycastAll((Vector2)world2DPosition, Vector2.zero, 0, mask);
                int hitCount2d = Physics2D.RaycastNonAlloc((Vector2)world2DPosition, Vector2.zero, hitGameObjects2d, 0, mask);
                if (hitCount2d > 0)
                {
                    for (int i = 0; i < hitCount2d; ++i)
                        hitGameObjects.Add(hitGameObjects2d[i].collider.gameObject);

                    RaySortLowestY(hitGameObjects);

                    foreach (GameObject selectedObject in hitGameObjects)
                    {
                        if (selectedObject.GetComponent<SelectableClick>())
                            return selectedObject;
                    }
                }
            }
        }

        return null;
    }

    public static bool IsUIUnderPointer()
    {
        if (RaycastFromPointer() != null)
            return true;

        return false;
    }

    static RaycastResult? RaycastFromPointer()
    {
        //List<RaycastResult> raycastResults = new List<RaycastResult>();

        // Gives some kind of dictionary error when first played. Wrapping in try-catch to shut it up.
        try
        {
            var standaloneInputModule = EventSystem.current.currentInputModule as CustomStandaloneInputModule;

            if (standaloneInputModule != null)
            {
                RaycastResult result = standaloneInputModule.GetPointerData().pointerCurrentRaycast;

                if (result.gameObject != null)
                    return result;
            }
        }
        catch
        {
        }

        return null;
    }

    public static GameObject GetUIRaycastableUnderPointer()
    {
        if (currentRaycastFromPointer != null)
            return ((RaycastResult)currentRaycastFromPointer).gameObject;

        return null;
    }

    public static T GetUIUnderPointer<T>() where T : Selectable
    {
        if (currentRaycastFromPointer != null)
        {
            RaycastResult raycastResult = (RaycastResult)currentRaycastFromPointer;
            GameObject hoveredObj = raycastResult.gameObject;

            if (hoveredObj && hoveredObj.GetComponent<T>())
            {
                return hoveredObj.GetComponent<T>();
            }
            else if (hoveredObj && hoveredObj.transform.parent.gameObject.GetComponent<T>())
            {
                return hoveredObj.transform.parent.gameObject.GetComponent<T>();
            }
        }

        return null;
    }
}

public class Draggable : MonoBehaviour
{
    public virtual void OnRightMouseDrag() { }
}
