using UnityEngine;
using UnitySteer;
using System.Collections;


public class Vehicle: MonoBehaviour
{
	#region Internal state values
	Vector3 _smoothedAcceleration;
	Vector3 _smoothedPosition;
	#endregion

	#region Private fields
	Steering[] _steerings;

	[SerializeField]
	private bool _hasInertia = false;

	[SerializeField]
	/// <summary>
	/// Internally-assigned Mass for the vehicle.
	/// </summary>
	/// <remarks>
	/// This value will be disregarded if the object has a rigidbody, and
	/// that rigidbody's mass value will be used instead.
	////remarks>
	private float _internalMass = 1;
	
	[SerializeField]
	bool _isPlanar = false;
	
	[SerializeField]
	float _radius = 1;

	[SerializeField]
	float _speed = 0;

	[SerializeField]
	float _maxSpeed = 1;

	[SerializeField]
	float _maxForce = 10;
	

	/// <summary>
	/// Indicates if the behavior should move or not
	/// </summary>
	[SerializeField]
	bool _canMove = true;

	private Radar _radar;
	#endregion


	#region Public properties
	/// <summary>
	/// Indicates if the current vehicle can move
	/// </summary>
	public bool CanMove {
		get {
			return this._canMove;
		}
		set {
			_canMove = value;
		}
	}

	/// <summary>
	/// Does the vehicle continue going when there's no force applied to it?
	/// </summary>
	public bool HasInertia {
		get {
			return this._hasInertia;
		}
		set {
			_hasInertia = value;
		}
	}

	/// <summary>
	/// Does the vehicle move in Y space?
	/// </summary>
	public bool IsPlanar {
		get {
			return this._isPlanar;
		}
		set {
			_isPlanar = value;
		}
	}

	/// <summary>
	/// Mass for the vehicle
	/// </summary>
	/// <remarks>If the vehicle has a rigidbody, its mass will be updated if
	/// this property is set.</remarks>
	public float Mass {
		get
		{
			return (rigidbody != null) ? rigidbody.mass : _internalMass;
		}
		set
		{
			if( rigidbody != null )
			{
				rigidbody.mass = value;
			}
			else
			{
				_internalMass = value;
			}
		}
	}

	/// <summary>
	/// Maximum force that can be applied to the vehicle
	/// </summary>
	public float MaxForce {
		get {
			return this._maxForce;
		}
		set {
			_maxForce = Mathf.Clamp(value, 0, float.MaxValue);
		}
	}

	/// <summary>
	/// The vehicle's maximum speed
	/// </summary>
	public float MaxSpeed {
		get {
			return this._maxSpeed;
		}
		set {
			_maxSpeed = Mathf.Clamp(value, 0, float.MaxValue);
		}
	}

	/// <summary>
	/// Radar assigned to this vehicle
	/// </summary>
	public Radar Radar {
		get {
			if (this._radar == null)
			{
				_radar = this.GetComponent<Radar>();
			}
			return this._radar;
		}
	}


	/// <summary>
	/// Vehicle radius
	/// </summary>
	public float Radius {
		get {
			return _radius;
		}
		set {
			_radius = Mathf.Clamp(value, 0.01f, float.MaxValue);
		}
	}

	/// <summary>
	/// Current vehicle speed
	/// </summary>
	public float Speed {
		get {
			return _speed;
		}
		set {
			_speed = Mathf.Clamp(value, 0, MaxSpeed);
		}
	}

	/// <summary>
	/// Current vehicle velocity
	/// </summary>
	public Vector3 Velocity
	{
		get
		{
			return transform.forward * _speed;
		}
	}
	#endregion

	#region Methods
	void Start()
	{
		_steerings = this.GetComponents<Steering>();
	}

	void FixedUpdate()
	{
		var force = Vector3.zero;
		foreach (var steering in _steerings)
		{
			if (steering.enabled)
				force  += steering.WeighedForce;
		}
		ApplySteeringForce(force, Time.fixedDeltaTime);
	}

	/// <summary>
	/// Applies a steering force to this vehicle
	/// </summary>
	/// <param name="force">
	/// A force vector to apply<see cref="Vector3"/>
	/// </param>
	/// <param name="elapsedTime">
	/// How long has elapsed since the last update<see cref="System.Single"/>
	/// </param>
	private void ApplySteeringForce(Vector3 force, float elapsedTime)
	{
		if (MaxForce == 0 || MaxSpeed == 0 || elapsedTime == 0)
		{
			return;
		}

		// enforce limit on magnitude of steering force
		Vector3 clippedForce = Vector3.ClampMagnitude(force, MaxForce);

		// compute acceleration and velocity
		Vector3 newAcceleration = (clippedForce / Mass);

		if (newAcceleration.sqrMagnitude == 0 && !HasInertia)
		{
			Speed = 0;
		}

		Vector3 newVelocity = Velocity;
		
		/*
			Damp out abrupt changes and oscillations in steering acceleration
			(rate is proportional to time step, then clipped into useful range)
			
			The lower the smoothRate parameter, the more noise there is
			likely to be in the movement.
		 */
		_smoothedAcceleration = OpenSteerUtility.blendIntoAccumulator(0.4f,
									newAcceleration,
									_smoothedAcceleration);

		// Euler integrate (per frame) acceleration into velocity
		newVelocity += _smoothedAcceleration * elapsedTime;

		// enforce speed limit
		newVelocity = Vector3.ClampMagnitude(newVelocity, MaxSpeed);

		if (IsPlanar)
		{
			newVelocity.y = Velocity.y;
		}

		// update Speed
		Speed = newVelocity.magnitude;
		
		

		// Euler integrate (per frame) velocity into position
		// TODO: Change for a motor
		if (rigidbody == null)
		{
			transform.position += (newVelocity * elapsedTime);
		}
		else
		{
			/*
			 * TODO: This is just a quick test and should not remain, as the behavior is not
			 * consistent to that we obtain when moving the transform.
			 */
			rigidbody.AddForce(Speed * elapsedTime * transform.forward, ForceMode.Impulse);
		}
		

		// regenerate local space (by default: align vehicle's forward axis with
		// new velocity, but this behavior may be overridden by derived classes.)
		RegenerateLocalSpace (newVelocity);
		
		// running average of recent positions
		_smoothedPosition = OpenSteerUtility.blendIntoAccumulator(elapsedTime * 0.06f,
								transform.position,
								_smoothedPosition);
	}
	
	protected virtual void RegenerateLocalSpace (Vector3 newVelocity)
	{
 		if (Speed > 0)
			transform.forward = newVelocity / Speed;
	}	
	
	/// <summary>
	/// Adjust the steering force passed to ApplySteeringForce.
	/// </summary>
	/// <param name="force">
	/// A force to be applied to the vehicle<see cref="Vector3"/>
	/// </param>
	/// <returns>
	/// Adjusted force vector <see cref="Vector3"/>
	/// </returns>
	/// <remarks>
	/// Allows a specific vehicle class to redefine this adjustment.
	/// The default will return the value unmodified.
	/// </remarks>
	protected virtual Vector3 AdjustRawSteeringForce(Vector3 force)
	{
		return force;
	}
	
	
	/// <summary>
	/// Predicts where the vehicle will be at a point in the future
	/// </summary>
	/// <param name="predictionTime">
	/// A time in seconds for the prediction <see cref="System.Single"/>
	/// </param>
	/// <returns>
	/// Vehicle position<see cref="Vector3"/>
	/// </returns>
	public virtual Vector3 PredictFuturePosition(float predictionTime)
    {
        return transform.position + (Velocity * predictionTime);
    }	
	
	
	/// <summary>
	/// Calculates if a vehicle is in the neighborhood of another
	/// </summary>
	/// <param name="other">
	/// Another vehicle to check against<see cref="Vehicle"/>
	/// </param>
	/// <param name="minDistance">
	/// Minimum distance <see cref="System.Single"/>
	/// </param>
	/// <param name="maxDistance">
	/// Maximum distance <see cref="System.Single"/>
	/// </param>
	/// <param name="cosMaxAngle">
	/// Cosine of the maximum angle between vehicles (for performance)<see cref="System.Single"/>
	/// </param>
	/// <returns>
	/// True if within the neighborhood, or false if otherwise<see cref="System.Boolean"/>
	/// </returns>
	/// <remarks>Originally SteerLibrary.inBoidNeighborhood</remarks>
	public bool IsInNeighborhood (Vehicle other, float minDistance, float maxDistance, float cosMaxAngle)
	{
		if (other == this)
		{
			return false;
		}
		else
		{
			Vector3 offset = other.transform.position - transform.position;
			float distanceSquared = offset.sqrMagnitude;

			// definitely in neighborhood if inside minDistance sphere
			if (distanceSquared < (minDistance * minDistance))
			{
				return true;
			}
			else
			{
				// definitely not in neighborhood if outside maxDistance sphere
				if (distanceSquared > (maxDistance * maxDistance))
				{
					return false;
				}
				else
				{
					// otherwise, test angular offset from forward axis
					Vector3 unitOffset = offset / (float) System.Math.Sqrt (distanceSquared);
					float forwardness = Vector3.Dot(transform.forward, unitOffset);
					return forwardness > cosMaxAngle;
				}
			}
		}
	}
		
	
	/// <summary>
	/// Returns a vector to seek a target position
	/// </summary>
	/// <param name="target">
	/// Target position <see cref="Vector3"/>
	/// </param>
	/// <returns>
	/// Seek vector <see cref="Vector3"/>
	/// </returns>
	public Vector3 GetSeekVector(Vector3 target)
	{
		/*
		 * First off, we calculate how far we are from the target, If this
		 * distance is smaller than the configured vehicle radius, we tell
		 * the vehicle to stop.
		 */
		Vector3 force = Vector3.zero;
        float d = Vector3.Distance(transform.position, target);
        if (d > Radius)
		{
			/*
			 * But suppose we still have some distance to go. The first step
			 * then would be calculating the steering force necessary to orient
			 * ourselves to and walk to that point.  The steerForSeek function
			 * takes into account values luke the MaxForce to apply and the 
			 * vehicle's MaxSpeed, and returns a steering vector.
			 * 
			 * It doesn't apply the steering itself, simply returns the value so
			 * we can continue operating on it.
			 */
			force = target - transform.position - Velocity;
		}
		return force;
		
	}
	
	/// <summary>
	/// Resets the vehicle's orientation.
	/// </summary>
	public void ResetOrientation()
	{
		transform.up = Vector3.up;
		transform.forward = Vector3.forward;
	}
	#endregion
}