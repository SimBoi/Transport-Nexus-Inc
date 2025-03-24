using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public enum CartType
{
    Locomotive,
    Fluid,
    Cargo
}

public class Train : MonoBehaviour
{
    public int id;
    [SerializeField] private Collider clickCollider;

    [Header("Carts")]
    [SerializeField] private GameObject locomotivePrefab;
    [SerializeField] private GameObject fluidCartPrefab;
    [SerializeField] private GameObject cargoCartPrefab;
    public List<CartType> cartTypes { get; private set; } = new List<CartType>();
    public List<GameObject> carts { get; private set; } = new List<GameObject>();

    [Header("Movement")]
    public float maxSpeed = 1;
    public float speed = 0;
    public float acceleration = 0.1f;
    public float deceleration = 0.1f;
    private float actualDeceleration;
    public bool isBraking = true;
    public bool isCrashed = false;

    [Header("Path")]
    private List<Vector2Int> tilesPath = new(); // the 2d tiles along the path, the head of the train is always on the last tile
    private List<Vector3> pathHalfSegments = new(); // the 3d points defining the half segments along the path
    private float headInterpolation; // the interpolation value between the first and last points in the path half segments list for the head of the train's position, each half segment is 1 unit long in interpolation space
    private Vector2Int headOrientation; // the orientation of the head of the train

    public void Initialize(Vector2Int initialTile, Vector2Int initialOrientation)
    {
        tilesPath.Add(initialTile);
        Vector2 halfOrientation = new Vector2(initialOrientation.x, initialOrientation.y) * 0.5f;
        pathHalfSegments.Add(new Vector3(initialTile.x - halfOrientation.x, 0, initialTile.y - halfOrientation.y));
        pathHalfSegments.Add(new Vector3(initialTile.x, 0, initialTile.y));
        pathHalfSegments.Add(new Vector3(initialTile.x + halfOrientation.x, 0, initialTile.y + halfOrientation.y));
        headInterpolation = 2;
        headOrientation = initialOrientation;
        AddCart(CartType.Locomotive);

        GameManager.Instance.TrainEnterTile(this, initialTile);
    }

    private void Update()
    {
        if (isCrashed) return;

        // update the speed of the train
        if (isBraking) speed = Mathf.Max(0, speed - actualDeceleration * Time.deltaTime);
        else speed = Mathf.Min(maxSpeed, speed + acceleration * Time.deltaTime);

        // move the head of the train along the half segments path
        headInterpolation += speed * Time.deltaTime;

        // if the head of the train crosses the end of the path, remove the first tile and add a new one at the end
        if (headInterpolation > pathHalfSegments.Count - 1) AdvancePath(tilesPath[tilesPath.Count - 1] + headOrientation);

        // update the positions of the carts
        transform.position = InterpolatePath(headInterpolation);
        for (int i = 0; i < carts.Count; i++)
        {
            (Vector3 cartPosition, Quaternion cartRotation) = InterpolateCart(i);
            carts[i].transform.position = cartPosition;
            carts[i].transform.rotation = cartRotation;
        }
    }

    private void AdvancePath(Vector2Int newTile)
    {
        // get the allowed train orientations for the new tile
        List<Vector2Int> newOrientations = GameManager.Instance.GetTrainOrientations(newTile);

        // if the new tile is compatible with the current head orientation, set the new head orientation and add the new tile and half segments
        if (newOrientations.Contains(headOrientation))
        {
            headOrientation = newOrientations[0] == headOrientation ? -newOrientations[1] : -newOrientations[0];
            Vector2 halfOrientation = new Vector2(headOrientation.x, headOrientation.y) * 0.5f;
            Vector3 newPathEnd = new Vector3(newTile.x + halfOrientation.x, 0, newTile.y + halfOrientation.y);

            // Step into the new tile
            tilesPath.Add(newTile);
            pathHalfSegments.Add(new Vector3(newTile.x, 0, newTile.y));
            pathHalfSegments.Add(newPathEnd);
            GameManager.Instance.TrainEnterTile(this, newTile);

            StepOutOfTile(0);
        }
        else
        {
            Crash();
            headInterpolation = pathHalfSegments.Count - 1;
        }
    }

    private void StepOutOfTile(int index)
    {
        GameManager.Instance.TrainExitTile(this, tilesPath[index]);
        headInterpolation -= 2;
        tilesPath.RemoveAt(index);
        pathHalfSegments.RemoveAt(index);
        pathHalfSegments.RemoveAt(index);
    }

    private (Vector3, Quaternion) InterpolateCart(int index)
    {
        float centerInterpolation = headInterpolation - 1 - 2 * index;
        Vector3 frontPoint = InterpolatePath(centerInterpolation - 0.5f);
        Vector3 backPoint = InterpolatePath(centerInterpolation + 0.5f);
        Vector3 center = (frontPoint + backPoint) / 2;
        Vector3 direction = backPoint - frontPoint;
        Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
        return (center, rotation);
    }

    private Vector3 InterpolatePath(float interpolation)
    {
        int nextSegment = Mathf.CeilToInt(interpolation);
        int prevSegment = Mathf.FloorToInt(interpolation);
        if (nextSegment == prevSegment) prevSegment--;

        // interpolate between the two half segments from 0 to 1
        interpolation -= prevSegment;
        return Vector3.Lerp(pathHalfSegments[prevSegment], pathHalfSegments[nextSegment], interpolation);
    }

    public void Accelerate()
    {
        if (isCrashed) return;
        isBraking = false;
    }

    public void Brake(float maxHalfSegments = 0)
    {
        isBraking = true;
        if (maxHalfSegments != 0)
        {
            // calculate the distance the train will travel while braking at the current deceleration
            // if the train is too close to the end of the path, use a stronger deceleration
            // a = - v^2 / (2 * d)
            float maxBrakingDistance = maxHalfSegments - (headInterpolation - Mathf.Floor(headInterpolation));
            float requiredDeceleration = speed * speed / (2 * maxBrakingDistance);
            actualDeceleration = requiredDeceleration;
        }
        else
        {
            actualDeceleration = deceleration;
        }
    }

    public bool AddCart(CartType cartType, int index = -1)
    {
        if (isCrashed) return false;

        Vector3 firstHalfSegmentDirection = (pathHalfSegments[1] - pathHalfSegments[0]).normalized;
        Vector2Int tailOrientation = new Vector2Int(Mathf.RoundToInt(firstHalfSegmentDirection.x), Mathf.RoundToInt(firstHalfSegmentDirection.z));
        Vector2Int newTile = tilesPath[0] - tailOrientation;
        List<Vector2Int> newOrientations = GameManager.Instance.GetTrainOrientations(newTile);
        if (!GameManager.Instance.CanBuildTrain(newTile)) return false;

        // add a new tile at the tail of the train
        tailOrientation = tailOrientation == -newOrientations[0] ? newOrientations[1] : newOrientations[0];
        Vector2 halfOrientation = new Vector2(tailOrientation.x, tailOrientation.y) * 0.5f;
        tilesPath.Insert(0, newTile);
        pathHalfSegments.Insert(0, new Vector3(newTile.x - halfOrientation.x, 0, newTile.y - halfOrientation.y));
        pathHalfSegments.Insert(1, new Vector3(newTile.x, 0, newTile.y));
        headInterpolation += 2;
        GameManager.Instance.TrainEnterTile(this, newTile);

        // add the new cart
        GameObject newCart;
        if (cartType == CartType.Locomotive) newCart = Instantiate(locomotivePrefab, transform);
        else if (cartType == CartType.Fluid) newCart = Instantiate(fluidCartPrefab, transform);
        else newCart = Instantiate(cargoCartPrefab, transform);
        newCart.GetComponent<Cart>().train = this;

        if (index >= 0 && index < carts.Count)
        {
            cartTypes.Insert(index, cartType);
            carts.Insert(index, newCart);
        }
        else
        {
            cartTypes.Add(cartType);
            carts.Add(newCart);
        }

        // handle the collider pointer click event in the train UI
        EventTrigger.Entry pointerClickEvent = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick, callback = new EventTrigger.TriggerEvent() };
        pointerClickEvent.callback.AddListener(GetComponent<TrainUI>().OnPointerClick);
        newCart.AddComponent<EventTrigger>().triggers = new List<EventTrigger.Entry> { pointerClickEvent };

        return true;
    }

    public void RemoveCart(int index)
    {
        if (speed > 0 || headInterpolation != pathHalfSegments.Count - 1) throw new System.Exception("Cannot remove carts while the train is moving");

        // TODO
    }

    public void Crash()
    {
        // TODO
        Debug.Log("Crash!");
        speed = 0;
        isBraking = true;
        isCrashed = true;
    }

    public void DestroyTrain()
    {
        for (int i = tilesPath.Count; i > 0; i--) StepOutOfTile(0);
        Destroy(gameObject);
    }
}
