using UnityEngine;

public class Disk : MonoBehaviour
{
    private static readonly int IsSetColor = Animator.StringToHash("isSetColor");
    private static readonly int IsBlack = Animator.StringToHash("isBlack");

    [SerializeField] private Animator _animator;

    public void SetColor(bool isBlack)
    {
        _animator.SetBool(IsBlack, isBlack);
        _animator.SetBool(IsSetColor, true);
    }

    public void Flip(bool isBlack)
    {
        _animator.SetBool(IsBlack, isBlack);
    }
}
