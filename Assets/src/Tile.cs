using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class Tile : MonoBehaviour
{
    [SerializeField] private MeshFilter insideMeshFilter;
    [SerializeField] private TMP_Text _hintText;

    private ISetDisk _setDisk;
    private int _x;
    private int _y;
    private Transform _inside;

    public void Init(ISetDisk setDisk, int x, int y)
    {
        _setDisk = setDisk;
        _x = x;
        _y = y;

        _inside = insideMeshFilter.transform;
        var col = _inside.gameObject.AddComponent<MeshCollider>();
        col.sharedMesh = insideMeshFilter.sharedMesh;
    }

    public void ShowHint(int count)
    {
        _hintText.text = count.ToString();
        _hintText.gameObject.SetActive(true);
    }

    public void ClearHint()
    {
        _hintText.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (Pointer.current == null || !Pointer.current.press.wasPressedThisFrame)
            return;
        if (Camera.main == null)
            return;
        Ray ray = Camera.main.ScreenPointToRay(Pointer.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit))
            return;
        if (hit.transform != _inside)
            return;
        _setDisk.SetDisk(_x, _y, transform);
    }
}
