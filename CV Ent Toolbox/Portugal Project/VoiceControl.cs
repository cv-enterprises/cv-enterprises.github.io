using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Windows.Speech;

public class VoiceControl : MonoBehaviour
{
    public string[] keywords = new string[] { "zoom in", "zoom out", "go left", "go right", "back","exit","Berlin", "Dubai", "India", "Los Angeles", "Lisbon", "Moscow", "Kilimanjaro", "New York", "Rio", "Tokyo",
    "Cambodia","China","Egypt","Lima","Niagra Falls","Paris","Rome","Seattle","Sydney","World tour"};

    public ConfidenceLevel confidence = ConfidenceLevel.Low;
    public Text results;
    protected PhraseRecognizer recognizer;
    protected string word = "";

    [SerializeField]
    private float numOfPics = 4;
    [SerializeField]
    private int LEFT_ROTATION = -90;
    [SerializeField]
    private int ZOOM_IN = -5;
    [SerializeField]
    private float zoomDuration = 3;
    [SerializeField]
    private float rotDuration = 3;

    [SerializeField]
    private Vector3 MAP_POSITION = new Vector3(-90, 85, -65);
    [SerializeField]
    private Vector3 ORIGINAL_POSITION = new Vector3(0, 0, -300);

    [SerializeField]
    private Camera cam;

    private bool isZooming;
    private bool isRotating;
    private bool goingToCity;

    public MapLocation[] _locations;
    public Dictionary<string, MapLocation> _locationDictionary = new Dictionary<string, MapLocation>();

    private Queue<rotCommand> rotQueue = new Queue<rotCommand>();
    private Queue<zoomCommand> zoomQueue = new Queue<zoomCommand>();
    private Queue<Vector3> cityQueue = new Queue<Vector3>();

    private enum rotCommand { goLeft, goRight }
    private enum zoomCommand { zoomIn, zoomOut }

    private void Start()
    {
        if (keywords != null)
        {
            recognizer = new KeywordRecognizer(keywords, confidence);
            recognizer.OnPhraseRecognized += Recognizer_OnPhraseRecognized;
            recognizer.Start();
        }

        for (int i = 0; i < _locations.Length; i++)
        {
            var location = _locations[i];
            _locationDictionary.Add(location.name, location);
        }
    }

    private void Recognizer_OnPhraseRecognized(PhraseRecognizedEventArgs args)
    {
        word = args.text;
        results.text = "You said: " + word;

        if (word == "exit"|| (cam.transform.position.z == MAP_POSITION.z && word == "back"))
        {
            StopAllCoroutines();cityQueue.Clear();
            StartCoroutine(MapZoom(ORIGINAL_POSITION));
        }

        else if (cam.transform.position == ORIGINAL_POSITION)
        {
            switch (word)
            {
                case "zoom in":
                    if(cam.fieldOfView>=10)
                    {
                        zoomQueue.Enqueue(zoomCommand.zoomIn);
                        CheckZoom();
                        results.text = "zoom in";
                    }
                   break;
                case "zoom out":
                    if(cam.fieldOfView<=50)
                    {
                        zoomQueue.Enqueue(zoomCommand.zoomOut);
                        CheckZoom();
                        results.text = "zoom out";
                    }
                    break;
                case "go left":
                    rotQueue.Enqueue(rotCommand.goLeft);
                    CheckRot();
                    break;
                case "go right":
                    rotQueue.Enqueue(rotCommand.goRight);
                    CheckRot();
                    break;
            }
        }

        else if (cam.transform.position.z >= MAP_POSITION.z)
        {
            MapLocation location;
            if (_locationDictionary.TryGetValue(word, out location))
            {
                if (cam.transform.position.z > MAP_POSITION.z)
                {
                    cityQueue.Enqueue(MAP_POSITION);
                    CheckCity();
                }
                cityQueue.Enqueue(location.Coordinates);
                CheckCity();
                
            }
            else if (word == "World tour")
            {
                foreach (MapLocation loc in _locations)
                {
                    cityQueue.Enqueue(loc.Coordinates);
                    CheckCity();
                    cityQueue.Enqueue(MAP_POSITION);
                }
            }

            else if (cam.transform.position.z > MAP_POSITION.z && word == "back")
            {
                StopAllCoroutines(); cityQueue.Clear();
                StartCoroutine(MapZoom(MAP_POSITION));
            }
        }

        else{results.text = "Please try again";}
    }

    public void CheckRot() { if (rotQueue.Count == 1 && !isRotating){ NextRotCommand(); } }
    public void CheckZoom() { if (zoomQueue.Count == 1 && !isZooming) { NextZoomCommand(); } }
    public void CheckCity() { if (cityQueue.Count == 1 && !goingToCity) { NextCityCommand(); } }

    private void OnApplicationQuit()
    {
        if (recognizer != null && recognizer.IsRunning)
        {
            recognizer.OnPhraseRecognized -= Recognizer_OnPhraseRecognized;
            recognizer.Stop();
        }
    }

    public void NextZoomCommand()
    {
        if (zoomQueue.Count > 0)
        {
            var zoom = zoomQueue.Dequeue();
            StartCoroutine(ZoomRoutine(zoom == zoomCommand.zoomIn ? ZOOM_IN : -ZOOM_IN));
        }
    }

    public IEnumerator ZoomRoutine(float amount)
    {
        isZooming = true;
        float time = Time.time;
        float endTime = time + zoomDuration;
        while (Time.time < endTime)
        {
            float move = (amount / zoomDuration) * Time.deltaTime;
            Camera.main.fieldOfView += move;
            yield return null;
        }
        isZooming = false;

        NextZoomCommand();
    }

    public void NextRotCommand()
    {
        if (rotQueue.Count > 0)
        {
            var direction = rotQueue.Dequeue();
            StartCoroutine(RotateRoutine(direction == rotCommand.goLeft ? LEFT_ROTATION : -LEFT_ROTATION));
        }
    }

    public IEnumerator RotateRoutine(float angle)
    {
        isRotating = true;
        float time = Time.time;
        float endTime = time + rotDuration;
        while (Time.time < endTime)
        {
            float rotate = (angle / rotDuration) * Time.deltaTime;
            cam.transform.Rotate(cam.transform.up * rotate);
            yield return null;
        }
        var yRot = cam.transform.eulerAngles.y;
        float y = Mathf.Round(yRot /= 90f) * (360 / numOfPics);
        cam.transform.rotation = Quaternion.Euler(0, y, 0);
        isRotating = false;

        NextRotCommand();
    }

    public void NextCityCommand()
    {
        if (cityQueue.Count > 0)
        {
            var loc = cityQueue.Dequeue();
            StartCoroutine(MapZoom(loc));
        }
    }

    public IEnumerator MapZoom(Vector3 amount)
    {
        goingToCity = true;
        float time = Time.time;
        float endTime = time + zoomDuration;
        Vector3 diff = amount - cam.transform.position;
        while (Time.time < endTime)
        {
            Vector3 move = (diff / zoomDuration) * Time.deltaTime;
            cam.transform.position += move;
            yield return null;
        }

        cam.transform.position = amount;
        goingToCity = false; 

        if (cam.transform.position.z > -1 && cityQueue.Count > 0)
        {
            results.text = "Waiting";
            yield return new WaitForSeconds(2);
            results.text = "You said: " + word;
        }

        NextCityCommand();
    }

    public void GoToMapCanvas(){ StopAllCoroutines(); StartCoroutine(MapZoom(MAP_POSITION));}
    public void GoToOriginalPosition(){ StopAllCoroutines(); StartCoroutine(MapZoom(ORIGINAL_POSITION));}
}
