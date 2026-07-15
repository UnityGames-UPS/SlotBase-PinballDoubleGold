using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ImageAnimation : MonoBehaviour
{
	public enum ImageState
	{
		NONE,
		PLAYING,
		PAUSED
	}

	public static ImageAnimation Instance;

	public List<Sprite> textureArray;

	public Image rendererDelegate;

	public bool useSharedMaterial = true;

	public bool doLoopAnimation = true;
	public bool IsComplete { get; private set; }
	[SerializeField] private bool StartOnAwake;

	[HideInInspector]
	public ImageState currentAnimationState;

	private int indexOfTexture;

	[SerializeField] private float idealFrameRate = 0.0416666679f;

	private float delayBetweenAnimation;

	public float AnimationSpeed = 5f;

	public float delayBetweenLoop;

	private Coroutine _animRoutine;

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
		}
		if (StartOnAwake)
		{
			StartAnimation();
		}
	}

	private void OnDisable()
	{
		StopAnimation();
	}

	public void StartAnimation()
	{
		if (textureArray == null || textureArray.Count == 0) return;
		if (_animRoutine != null)
		{
			StopCoroutine(_animRoutine);
			_animRoutine = null;
		}
		indexOfTexture = 0;
		IsComplete = false;
		RevertToInitialState();
		delayBetweenAnimation = idealFrameRate * (float)textureArray.Count / AnimationSpeed;
		currentAnimationState = ImageState.PLAYING;
		_animRoutine = StartCoroutine(AnimationRoutine());
	}

	private IEnumerator AnimationRoutine()
	{
		while (true)
		{
			yield return new WaitForSeconds(delayBetweenAnimation);
			SetTextureOfIndex();
			indexOfTexture++;
			if (indexOfTexture == textureArray.Count)
			{
				indexOfTexture = 0;
				if (doLoopAnimation)
				{
					if (delayBetweenLoop > 0f)
						yield return new WaitForSeconds(delayBetweenLoop);
				}
				else
				{
					IsComplete = true;
					currentAnimationState = ImageState.NONE;
					_animRoutine = null;
					yield break;
				}
			}
		}
	}

	public void PauseAnimation()
	{
		if (currentAnimationState == ImageState.PLAYING)
		{
			if (_animRoutine != null)
			{
				StopCoroutine(_animRoutine);
				_animRoutine = null;
			}
			currentAnimationState = ImageState.PAUSED;
		}
	}

	public void ResumeAnimation()
	{
		if (currentAnimationState == ImageState.PAUSED && _animRoutine == null)
		{
			_animRoutine = StartCoroutine(AnimationRoutine());
			currentAnimationState = ImageState.PLAYING;
		}
	}

	public void StopAnimation()
	{
		if (_animRoutine != null)
		{
			StopCoroutine(_animRoutine);
			_animRoutine = null;
		}
		currentAnimationState = ImageState.NONE;
		IsComplete = false;
		indexOfTexture = 0;
		if (rendererDelegate && textureArray != null && textureArray.Count > 0)
			rendererDelegate.sprite = textureArray[0];
	}

	public void RevertToInitialState()
	{
		indexOfTexture = 0;
		SetTextureOfIndex();
	}

	public float GetTotalDuration()
	{
		float delay = idealFrameRate * textureArray.Count / AnimationSpeed;
		return delay * textureArray.Count;
	}

	private void SetTextureOfIndex()
	{
		if (rendererDelegate == null) return;
		rendererDelegate.sprite = textureArray[indexOfTexture];
	}
}
