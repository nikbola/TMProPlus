using UnityEngine;
using TMPro;
using Unity.VisualScripting;
using System.Collections;

public class TextIterator : MonoBehaviour
{
    public delegate void CharacterTyped(char character);
    public event CharacterTyped OnCharacterTyped;

    #region Serialized Fields
    [SerializeField] TextMeshProUGUI _textMeshComponent;
    [SerializeField] bool _iterateOnChange;
    [SerializeField] bool _skipSpaces = true;
    [SerializeField] bool _fadeInCharacters;
    [SerializeField] bool _useOffset;
    [SerializeField] Vector3 _offset;
    [SerializeField] bool _fromPosition;
    [SerializeField] Transform _sourcePosition;
    [SerializeField] float _secondsPerCharacter;
    #endregion

    #region Public Fields
    public bool iterateOnChange 
    { 
        get { return _iterateOnChange; } 
        set { _iterateOnChange = value; } 
    }
    public bool skipSpaces 
    { 
        get { return _skipSpaces; } 
        set { _skipSpaces = value; } 
    }
    public bool fadeInCharacters 
    { 
        get { return _fadeInCharacters; } 
        set { _fadeInCharacters = value; } 
    }
    public bool useOffset
    {
        get { return _useOffset; }
        set { _useOffset = value; }
    }
    public Vector3 offset
    {
        get { return _offset; }
        set { _offset = value; }
    }
    public bool fromPosition
    {
        get { return _fromPosition; }
        set { _fromPosition = value; }
    }
    public Transform sourcePosition
    {
        get { return _sourcePosition; }
        set { _sourcePosition = value; }
    }
    public float secondsPerCharacter { 
        get { return _secondsPerCharacter; } 
        set { _secondsPerCharacter = value; } 
    }
    #endregion

    private bool _valueChanged = false;
    private string _previousValue = "";

    private void Awake()
    {
        _textMeshComponent ??= GetComponent<TextMeshProUGUI>();
        if (_textMeshComponent.IsUnityNull())
        {
            Debug.LogError($"Text Mesh Component has not been assigned, " +
                $"and no TextMeshPro component was found on {gameObject.name}");
        }
        
        _previousValue = _textMeshComponent.text;
    }

    private void Update()
    {
        _valueChanged = (_previousValue == _textMeshComponent.text) ? false : true;
        if (_iterateOnChange && _valueChanged)
        {
            StopAllCoroutines();
            IterateText();
        }
        _previousValue = _textMeshComponent.text;
    }

    // public wrapper to avoid having to call a coroutine when called from other scripts
    public void IterateText() 
    {
        StartCoroutine(IterateTextCoroutine());
    }

    private IEnumerator IterateTextCoroutine() 
    {
        Color32 color = _textMeshComponent.color;
        int visibleIndex = 0;

        _textMeshComponent.ForceMeshUpdate();
        while (true)
        {
            // Exit loop if all characters have been iterated through
            if (visibleIndex >= _textMeshComponent.textInfo.characterCount)
            {
                break;
            }

            // Don't expend time on "drawing" spaces
            if (_skipSpaces)
            {
                returnPoint:
                if (_textMeshComponent.text[visibleIndex] == ' ')
                {
                    visibleIndex++;
                    goto returnPoint; //BuT GoToS aRe EvIl
                }
            }

            int visibleVertexIndex = 0;
            int visibleMeshIndex = 0;
            Color32[] visibleVertexColors = null;

            // Making all chars before the one currently being typed visible, and the ones after invisible
            for (int i = 0; i < _textMeshComponent.textInfo.characterCount; i++)
            {
                if (_textMeshComponent.textInfo.characterInfo[i].isVisible)
                {
                    color.a = (i <= visibleIndex) ? (byte)255 : (byte)0;
                    int meshIndex = _textMeshComponent.textInfo.characterInfo[i].materialReferenceIndex;
                    int vertexIndex = _textMeshComponent.textInfo.characterInfo[i].vertexIndex;
                    Color32[] vertexColors = _textMeshComponent.textInfo.meshInfo[meshIndex].colors32;

                    if (i == visibleIndex) 
                    { 
                        visibleVertexColors = vertexColors;
                        visibleVertexIndex = vertexIndex;
                        visibleMeshIndex = meshIndex;
                    }

                    for (int j = 0; j < 4; j++) vertexColors[vertexIndex + j] = color;
                }
            }

            // Copy values instead of reference
            Vector3[] vertexPositions = new Vector3[4];
            TMP_MeshInfo meshInfo = _textMeshComponent.textInfo.meshInfo[visibleMeshIndex];
            for (int i = 0; i < vertexPositions.Length; i++)
            {
                vertexPositions[i] = meshInfo.vertices[visibleVertexIndex + i];
            }

            // Get width and height to calculate vertex offset from center
            float width = 0f;
            float height = 0f;
            if (_fromPosition)
            {
                TMP_CharacterInfo characterInfo = _textMeshComponent.textInfo.characterInfo[visibleIndex];
                width = Vector3.Distance(characterInfo.topLeft, characterInfo.topRight);
                height = Vector3.Distance(characterInfo.topLeft, characterInfo.bottomLeft);
            }

            float elapsed = 0f;
            while (elapsed < _secondsPerCharacter)
            {
                // Make the char currently being typed fade from transparent to opaque
                if (_fadeInCharacters)
                {
                    float percent = Mathf.Lerp(0, 1, elapsed / _secondsPerCharacter);
                    int value = (int)(255 * percent);
                    color.a = (byte)value;
                    for (int i = 0; i < 4; i++)
                    {
                        visibleVertexColors[visibleVertexIndex + i] = color;
                    }
                }

                // Interpolate current char's position from source to destination
                if (_useOffset || _fromPosition)
                {
                    meshInfo = _textMeshComponent.textInfo.meshInfo[visibleMeshIndex];
                    Vector3[] startPositions = new Vector3[4];

                    // Offset each vertex from the center
                    if (_fromPosition)
                    {
                        Vector3 offsetY = new Vector3(0, height / 2, 0);
                        Vector3 offsetX = new Vector3(width / 2, 0, 0);
                        startPositions[0] = _sourcePosition.position - offsetY - offsetX; //
                        startPositions[1] = _sourcePosition.position + offsetY - offsetX; // ORDER IS
                        startPositions[2] = _sourcePosition.position + offsetY + offsetX; // IMPORTANT!
                        startPositions[3] = _sourcePosition.position - offsetY + offsetX; //

                        // Convert to local space
                        for (int i = 0; i < startPositions.Length; i++)
                        {
                            startPositions[i] = transform.InverseTransformPoint(startPositions[i]);
                        }
                    }
                    
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 startPos;

                        // From offset based on typed character's destination
                        if (_useOffset) startPos = vertexPositions[i] + offset;

                        // From absolute position
                        else startPos = startPositions[i];
                        
                        // Lerp each vertex position from source to destination
                        Vector3 currentPos = Vector3.Lerp(
                            startPos,
                            vertexPositions[i],
                            elapsed / _secondsPerCharacter
                        );
                        meshInfo.vertices[visibleVertexIndex + i] = currentPos;
                    }
                }
                _textMeshComponent.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Make sure that the character gets placed at exactly at destination after lerping is done
            if (_useOffset || _fromPosition)
            {
                meshInfo = _textMeshComponent.textInfo.meshInfo[visibleMeshIndex];

                for (int i = 0; i < 4; i++) meshInfo.vertices[visibleVertexIndex + i] = vertexPositions[i];
            }
            _textMeshComponent.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
            visibleIndex++;

            // Trigger On Character Typed Event
            if (OnCharacterTyped != null) OnCharacterTyped(_textMeshComponent.text[visibleIndex]);
        }
    }
}
