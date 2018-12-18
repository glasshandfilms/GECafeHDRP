// Amplify Occlusion 2 - Robust Ambient Occlusion for Unity
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

using System;
using System.Diagnostics;

using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

#if UNITY_EDITOR
using UnityEditor.Rendering.PostProcessing;
#endif

using AmplifyOcclusion;


[Serializable]
public sealed class SampleCountLevelParameter : ParameterOverride<SampleCountLevel> {}


public enum ApplicationMethod
{
	PostEffect = 0,
	Debug
}

[Serializable]
public sealed class ApplicationMethodParameter : ParameterOverride<ApplicationMethod> {}

public enum PerPixelNormalSource
{
	None = 0,
	GBuffer
}

[Serializable]
public sealed class PerPixelNormalSourceParameter : ParameterOverride<PerPixelNormalSource> {}


[Serializable]
[PostProcess(typeof(AmplifyOcclusionRenderer), PostProcessEvent.AfterStack, "AmplifyCreations/AmplifyOcclusion")]
public sealed class AmplifyOcclusionPostProcessing : PostProcessEffectSettings
{
	[Header( "Ambient Occlusion" )]
	[Tooltip( "How to inject the occlusion: Post Effect = Overlay, Debug - Vizualize." )]
	public ApplicationMethodParameter ApplyMethod = new ApplicationMethodParameter{ value = ApplicationMethod.PostEffect };

	[Tooltip( "Number of samples per pass." )]
	public SampleCountLevelParameter SampleCount = new SampleCountLevelParameter{ value = SampleCountLevel.Medium };

	[Tooltip( "Source of per-pixel normals: None = All, GBuffer = Deferred." )]
	public PerPixelNormalSourceParameter PerPixelNormals = new PerPixelNormalSourceParameter{ value = PerPixelNormalSource.None };

	[Tooltip( "Final applied intensity of the occlusion effect." )]
	[Range( 0, 1 )]
	public FloatParameter Intensity = new FloatParameter{ value = 1.0f };

	[Tooltip( "Color tint for occlusion." )]
	public ColorParameter Tint = new ColorParameter{ value = Color.black };

	[Tooltip( "Radius spread of the occlusion." )]
	[Range( 0, 32 )]
	public FloatParameter Radius = new FloatParameter{ value = 2.0f };

	[Tooltip( "Power exponent attenuation of the occlusion." )]
	[Range( 0, 16 )]
	public FloatParameter PowerExponent = new FloatParameter{ value = 1.8f };

	[Tooltip( "Controls the initial occlusion contribution offset." )]
	[Range( 0, 0.99f )]
	public FloatParameter Bias = new FloatParameter{ value = 0.05f };

	[Tooltip( "Controls the thickness occlusion contribution." )]
	[Range( 0, 1.0f )]
	public FloatParameter Thickness = new FloatParameter{ value = 1.0f };

	[Tooltip( "Compute the Occlusion and Blur at half of the resolution." )]
	public BoolParameter Downsample = new BoolParameter{ value = true };

	[Tooltip( "Cache optimization for best performance / quality tradeoff." )]
	public BoolParameter CacheAware = new BoolParameter{ value = true };

	[Header( "Distance Fade" )]
	[Tooltip( "Control parameters at faraway." )]
	public BoolParameter FadeEnabled = new BoolParameter{ value = false };

	[Tooltip( "Distance in Unity unities that start to fade." )]
	public FloatParameter FadeStart = new FloatParameter{ value = 100.0f };

	[Tooltip( "Length distance to performe the transition." )]
	public FloatParameter FadeLength = new FloatParameter{ value = 50.0f };

	[Tooltip( "Final Intensity parameter." )]
	[Range( 0, 1 )]
	public FloatParameter FadeToIntensity = new FloatParameter{ value = 0.0f };
	public ColorParameter FadeToTint = new ColorParameter{ value = Color.black };

	[Tooltip( "Final Radius parameter." )]
	[Range( 0, 32 )]
	public FloatParameter FadeToRadius = new FloatParameter{ value = 2.0f };

	[Tooltip( "Final PowerExponent parameter." )]
	[Range( 0, 16 )]
	public FloatParameter FadeToPowerExponent = new FloatParameter{ value = 1.0f };

	[Tooltip( "Final Thickness parameter." )]
	[Range( 0, 1.0f )]
	public FloatParameter FadeToThickness = new FloatParameter{ value = 1.0f };

	[Header( "Bilateral Blur" )]
	public BoolParameter BlurEnabled = new BoolParameter{ value = true };

	[Tooltip( "Radius in screen pixels." )]
	[Range( 1, 4 )]
	public IntParameter BlurRadius = new IntParameter{ value = 3 };

	[Tooltip( "Number of times that the Blur will repeat." )]
	[Range( 1, 4 )]
	public IntParameter BlurPasses = new IntParameter{ value = 1 };

	[Tooltip( "Sharpness of blur edge-detection: 0 = Softer Edges, 20 = Sharper Edges." )]
	[Range( 0, 20 )]
	public FloatParameter BlurSharpness = new FloatParameter{ value = 15.0f };

	[Header( "Temporal Filter" )]
	[Tooltip( "Accumulates the effect over the time." )]
	public BoolParameter FilterEnabled = new BoolParameter{ value = true };

	[Tooltip( "Controls the accumulation decayment: 0 = More flicker with less ghosting, 1 = Less flicker with more ghosting." )]
	[Range( 0, 1 )]
	public FloatParameter FilterBlending = new FloatParameter{ value = 0.80f };

	[Tooltip( "Controls the discard sensitivity based on the motion of the scene and objects." )]
	[Range( 0, 1 )]
	public FloatParameter FilterResponse = new FloatParameter{ value = 0.5f };

	public override bool IsEnabledAndSupported(PostProcessRenderContext context)
	{
		return enabled.value;
	}
}

public sealed class AmplifyOcclusionRenderer : PostProcessEffectRenderer<AmplifyOcclusionPostProcessing>
{
	private TargetDesc m_target = new TargetDesc();


	void UpdateGlobalShaderConstants( CommandBuffer cb, Camera aCamera )
	{
		AmplifyOcclusionCommon.UpdateGlobalShaderConstants( cb, ref m_target, aCamera, settings.Downsample );
	}

	void UpdateGlobalShaderConstants_AmbientOcclusion( CommandBuffer cb )
	{
		// Ambient Occlusion
		cb.SetGlobalFloat( PropertyID._AO_Radius, settings.Radius );
		cb.SetGlobalFloat( PropertyID._AO_PowExponent, settings.PowerExponent );
		cb.SetGlobalFloat( PropertyID._AO_Bias, settings.Bias * settings.Bias );
		cb.SetGlobalColor( PropertyID._AO_Levels, new Color( settings.Tint.value.r, settings.Tint.value.g, settings.Tint.value.b, settings.Intensity ) );
		float invThickness = ( 1.0f - settings.Thickness );
		cb.SetGlobalFloat( PropertyID._AO_ThicknessDecay, ( 1.0f - invThickness * invThickness ) * 0.98f );

		if( settings.BlurEnabled == true )
		{
			cb.SetGlobalFloat( PropertyID._AO_BlurSharpness, settings.BlurSharpness * 100 );
		}

		// Distance Fade
		if( settings.FadeEnabled == true )
		{
			settings.FadeStart.value = Mathf.Max( 0.0f, settings.FadeStart );
			settings.FadeLength.value = Mathf.Max( 0.01f, settings.FadeLength );

			float rcpFadeLength = 1.0f / settings.FadeLength;

			cb.SetGlobalVector( PropertyID._AO_FadeParams, new Vector2( settings.FadeStart, rcpFadeLength ) );
			float invFadeThickness = ( 1.0f - settings.FadeToThickness );
			cb.SetGlobalVector( PropertyID._AO_FadeValues, new Vector4( settings.FadeToIntensity, settings.FadeToRadius, settings.FadeToPowerExponent, ( 1.0f - invFadeThickness * invFadeThickness ) * 0.98f ) );
			cb.SetGlobalColor( PropertyID._AO_FadeToTint, new Color( settings.FadeToTint.value.r, settings.FadeToTint.value.g, settings.FadeToTint.value.b, 0.0f ) );
		}
		else
		{
			cb.SetGlobalVector( PropertyID._AO_FadeParams, new Vector2( 0.0f, 0.0f ) );
		}

		if( UsingTemporalFilter == true )
		{
			AmplifyOcclusionCommon.CommandBuffer_TemporalFilterDirectionsOffsets( cb, m_sampleStep );
		}
		else
		{
			cb.SetGlobalFloat( PropertyID._AO_TemporalDirections, 0 );
			cb.SetGlobalFloat( PropertyID._AO_TemporalOffsets, 0 );
		}
	}


	private AmplifyOcclusionViewProjMatrix m_viewProjMatrix = new AmplifyOcclusionViewProjMatrix();
	
	void UpdateGlobalShaderConstants_Matrices( CommandBuffer cb, Camera aCamera )
	{
		m_viewProjMatrix.UpdateGlobalShaderConstants_Matrices( cb, aCamera, UsingTemporalFilter );
	}


	void PerformBlit( CommandBuffer cb, RenderTargetIdentifier destination, Material mat, int pass )
	{
		cb.SetRenderTargetWithLoadStoreAction( destination, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store );
		cb.DrawMesh( RuntimeUtilities.fullscreenTriangle, Matrix4x4.identity, mat, 0, pass );
	}

	void PerformBlit( CommandBuffer cb, Material mat, int pass )
	{
		cb.DrawMesh( RuntimeUtilities.fullscreenTriangle, Matrix4x4.identity, mat, 0, pass );
	}


	// Render Materials
	static private Material m_occlusionMat = null;
	static private Material m_blurMat = null;
	static private Material m_applyOcclusionMat = null;

	private void checkMaterials( bool aThroughErrorMsg )
	{
		if( m_occlusionMat == null )
		{
			m_occlusionMat = AmplifyOcclusionCommon.CreateMaterialWithShaderName( "Hidden/Amplify Occlusion/OcclusionPostProcessing", aThroughErrorMsg );
		}

		if( m_blurMat == null )
		{
			m_blurMat = AmplifyOcclusionCommon.CreateMaterialWithShaderName( "Hidden/Amplify Occlusion/BlurPostProcessing", aThroughErrorMsg );
		}

		if( m_applyOcclusionMat == null )
		{
			m_applyOcclusionMat = AmplifyOcclusionCommon.CreateMaterialWithShaderName( "Hidden/Amplify Occlusion/ApplyPostProcessing", aThroughErrorMsg );
		}
	}


	private RenderTextureFormat m_occlusionRTFormat = RenderTextureFormat.RGHalf;
	private RenderTextureFormat m_accumTemporalRTFormat = RenderTextureFormat.ARGB32;
	private RenderTextureFormat m_motionIntensityRTFormat = RenderTextureFormat.R8;

	private bool checkRenderTextureFormats()
	{
		// test the two fallback formats first
		if( SystemInfo.SupportsRenderTextureFormat( RenderTextureFormat.ARGB32 ) && SystemInfo.SupportsRenderTextureFormat( RenderTextureFormat.ARGBHalf ) )
		{
			m_occlusionRTFormat = RenderTextureFormat.RGHalf;
			if( !SystemInfo.SupportsRenderTextureFormat( m_occlusionRTFormat ) )
			{
				m_occlusionRTFormat = RenderTextureFormat.RGFloat;
				if( !SystemInfo.SupportsRenderTextureFormat( m_occlusionRTFormat ) )
				{
					// already tested above
					m_occlusionRTFormat = RenderTextureFormat.ARGBHalf;
				}
			}

			return true;
		}
		return false;
	}

	private RenderTexture m_occlusionDepthRT = null;
	private RenderTexture[] m_temporalAccumRT = null;
	private RenderTexture m_depthMipmap = null;

	private uint m_sampleStep = 0;
	private uint m_curStepIdx = 0;
	private string[] m_tmpMipString = null;
	private int m_numberMips = 0;

	private void commandBuffer_FillComputeOcclusion( CommandBuffer cb )
	{
		cb.BeginSample( "AO 1 - ComputeOcclusion" );

		Vector4 oneOverSize_Size = new Vector4( m_target.oneOverWidth,
												m_target.oneOverHeight,
												m_target.width,
												m_target.height );

		int sampleCountPass = ( (int)settings.SampleCount.value ) * AmplifyOcclusionCommon.PerPixelNormalSourceCount;

		int occlusionPass = ( ShaderPass.OcclusionLow_None +
							  sampleCountPass +
							  ( ( settings.PerPixelNormals == PerPixelNormalSource.None )?ShaderPass.OcclusionLow_None:ShaderPass.OcclusionLow_GBufferOctaEncoded ) );


		if( settings.CacheAware == true )
		{
			occlusionPass += ShaderPass.OcclusionLow_None_UseDynamicDepthMips;

			// Construct Depth mipmaps
			int previouslyTmpMipRT = 0;

			for( int i = 0; i < m_numberMips; i++ )
			{
				int tmpMipRT;

				int width = m_target.width >> ( i + 1 );
				int height = m_target.height >> ( i + 1 );

				tmpMipRT = AmplifyOcclusionCommon.SafeAllocateTemporaryRT( cb, m_tmpMipString[ i ],
																			width, height,
																			RenderTextureFormat.RFloat,
																			RenderTextureReadWrite.Linear,
																			FilterMode.Bilinear );

				// _AO_CurrDepthSource was previously set
				cb.SetRenderTarget( tmpMipRT );

				PerformBlit( cb, m_occlusionMat,  ( ( i == 0 )?ShaderPass.ScaleDownCloserDepthEven_CameraDepthTexture:ShaderPass.ScaleDownCloserDepthEven ) );

				cb.CopyTexture( tmpMipRT, 0, 0, m_depthMipmap, 0, i );

				if( previouslyTmpMipRT != 0 )
				{
					AmplifyOcclusionCommon.SafeReleaseTemporaryRT( cb, previouslyTmpMipRT );
				}

				previouslyTmpMipRT = tmpMipRT;

				cb.SetGlobalTexture( PropertyID._AO_CurrDepthSource, tmpMipRT ); // Set next MipRT ID
			}

			AmplifyOcclusionCommon.SafeReleaseTemporaryRT( cb, previouslyTmpMipRT );

			cb.SetGlobalTexture( PropertyID._AO_SourceDepthMipmap, m_depthMipmap );
		}


		if( settings.Downsample )
		{
			int halfWidth = m_target.width / 2;
			int halfHeight = m_target.height / 2;

			int tmpSmallOcclusionRT = AmplifyOcclusionCommon.SafeAllocateTemporaryRT( cb, "_AO_SmallOcclusionTexture",
																						halfWidth, halfHeight,
																						m_occlusionRTFormat,
																						RenderTextureReadWrite.Linear,
																						FilterMode.Bilinear );


			cb.SetGlobalVector( PropertyID._AO_Target_TexelSize, new Vector4( 1.0f / ( m_target.width / 2.0f ),
																			1.0f / ( m_target.height / 2.0f ),
																			m_target.width / 2.0f,
																			m_target.height / 2.0f ) );

			PerformBlit( cb, tmpSmallOcclusionRT, m_occlusionMat, occlusionPass );

			cb.SetRenderTarget( default( RenderTexture ) );
			cb.EndSample( "AO 1 - ComputeOcclusion" );

			if( settings.BlurEnabled == true )
			{
				commandBuffer_Blur( cb, tmpSmallOcclusionRT, halfWidth, halfHeight );
			}

			// Combine
			cb.BeginSample( "AO 2b - Combine" );

			cb.SetGlobalTexture( PropertyID._AO_CurrOcclusionDepth, tmpSmallOcclusionRT );

			cb.SetGlobalVector( PropertyID._AO_Target_TexelSize, oneOverSize_Size );

			PerformBlit( cb, m_occlusionDepthRT, m_occlusionMat, ShaderPass.CombineDownsampledOcclusionDepth );

			AmplifyOcclusionCommon.SafeReleaseTemporaryRT( cb, tmpSmallOcclusionRT );

			cb.SetRenderTarget( default( RenderTexture ) );
			cb.EndSample( "AO 2b - Combine" );
		}
		else
		{
			cb.SetGlobalVector( PropertyID._AO_Target_TexelSize, oneOverSize_Size );

			PerformBlit( cb, m_occlusionDepthRT, m_occlusionMat, occlusionPass );

			cb.SetRenderTarget( default( RenderTexture ) );
			cb.EndSample( "AO 1 - ComputeOcclusion" );

			if( settings.BlurEnabled == true )
			{
				commandBuffer_Blur( cb, m_occlusionDepthRT, m_target.width, m_target.height );
			}
		}
	}


	int commandBuffer_NeighborMotionIntensity( CommandBuffer cb, int aSourceWidth, int aSourceHeight )
	{
		int tmpRT = AmplifyOcclusionCommon.SafeAllocateTemporaryRT( cb, "_AO_IntensityTmp_" + aSourceWidth.ToString(),
																	aSourceWidth / 2, aSourceHeight / 2,
																	m_motionIntensityRTFormat,
																	RenderTextureReadWrite.Linear,
																	FilterMode.Bilinear );


		cb.SetGlobalVector( "_AO_Target_TexelSize", new Vector4( 1.0f / ( aSourceWidth / 2.0f ),
																 1.0f / ( aSourceHeight / 2.0f ),
																 aSourceWidth / 2.0f,
																 aSourceHeight / 2.0f ) );
		PerformBlit( cb, tmpRT, m_occlusionMat, ShaderPass.NeighborMotionIntensity );

		int tmpBlurRT = AmplifyOcclusionCommon.SafeAllocateTemporaryRT( cb, "_AO_BlurIntensityTmp",
																		aSourceWidth / 2, aSourceHeight / 2,
																		m_motionIntensityRTFormat,
																		RenderTextureReadWrite.Linear,
																		FilterMode.Bilinear );

		// Horizontal
		cb.SetGlobalTexture( PropertyID._AO_CurrMotionIntensity, tmpRT );
		PerformBlit( cb, tmpBlurRT, m_blurMat, ShaderPass.BlurHorizontalIntensity );

		// Vertical
		cb.SetGlobalTexture( PropertyID._AO_CurrMotionIntensity, tmpBlurRT );
		PerformBlit( cb, tmpRT, m_blurMat, ShaderPass.BlurVerticalIntensity );

		AmplifyOcclusionCommon.SafeReleaseTemporaryRT( cb, tmpBlurRT );

		cb.SetGlobalTexture( PropertyID._AO_CurrMotionIntensity, tmpRT );

		return tmpRT;
	}


	void commandBuffer_Blur( CommandBuffer cb, RenderTargetIdentifier aSourceRT, int aSourceWidth, int aSourceHeight )
	{
		cb.BeginSample( "AO 2 - Blur" );

		int tmpBlurRT = AmplifyOcclusionCommon.SafeAllocateTemporaryRT( cb, "_AO_BlurTmp",
																		aSourceWidth, aSourceHeight,
																		m_occlusionRTFormat,
																		RenderTextureReadWrite.Linear,
																		FilterMode.Point );

		// Apply Cross Bilateral Blur
		for( int i = 0; i < settings.BlurPasses; i++ )
		{
			// Horizontal
			cb.SetGlobalTexture( PropertyID._AO_CurrOcclusionDepth, aSourceRT );

			int blurHorizontalPass = ShaderPass.BlurHorizontal1 + ( settings.BlurRadius - 1 ) * 2;

			PerformBlit( cb, tmpBlurRT, m_blurMat, blurHorizontalPass );


			// Vertical
			cb.SetGlobalTexture( PropertyID._AO_CurrOcclusionDepth, tmpBlurRT );

			int blurVerticalPass = ShaderPass.BlurVertical1 + ( settings.BlurRadius - 1 ) * 2;

			PerformBlit( cb, aSourceRT, m_blurMat, blurVerticalPass );
		}

		AmplifyOcclusionCommon.SafeReleaseTemporaryRT( cb, tmpBlurRT );

		cb.SetRenderTarget( default( RenderTexture ) );
		cb.EndSample( "AO 2 - Blur" );
	}


	int getTemporalPass()
	{
		return  ( ( UsingMotionVectors == true ) ? ( 1 << 0 ) : 0 );
	}


	void commandBuffer_TemporalFilter( CommandBuffer cb )
	{
		if( ( m_clearHistory == true ) || ( m_paramsChanged == true ) )
		{
			clearHistory( cb );
		}

		// Temporal Filter
		float temporalAdj = Mathf.Lerp( 0.01f, 0.99f, settings.FilterBlending );

		cb.SetGlobalFloat( PropertyID._AO_TemporalCurveAdj, temporalAdj );
		cb.SetGlobalFloat( PropertyID._AO_TemporalMotionSensibility, settings.FilterResponse * settings.FilterResponse + 0.01f );

		cb.SetGlobalTexture( PropertyID._AO_CurrOcclusionDepth, m_occlusionDepthRT );
		cb.SetGlobalTexture( PropertyID._AO_TemporalAccumm, m_temporalAccumRT[ 1 - m_curStepIdx ] );
	}

	private RenderTargetIdentifier curTemporalRT;

	void commandBuffer_FillApplyPostEffect( CommandBuffer cb, RenderTargetIdentifier aSourceRT, RenderTargetIdentifier aDestinyRT )
	{
		cb.BeginSample( "AO 3 - ApplyPostEffect" );

		cb.SetGlobalTexture( PropertyID._MainTex, aSourceRT );

		if( UsingTemporalFilter )
		{
			commandBuffer_TemporalFilter( cb );

			int tmpMotionIntensityRT = 0;

			if( UsingMotionVectors == true )
			{
				tmpMotionIntensityRT = commandBuffer_NeighborMotionIntensity( cb, m_target.width, m_target.height );
			}

			applyPostEffectTargetsTemporal[ 0 ] = aDestinyRT;
			applyPostEffectTargetsTemporal[ 1 ] = new RenderTargetIdentifier( m_temporalAccumRT[ m_curStepIdx ] );

			cb.SetRenderTarget( applyPostEffectTargetsTemporal, applyPostEffectTargetsTemporal[ 0 ] /* Not used, just to make Unity happy */ );
			PerformBlit( cb, m_applyOcclusionMat, ShaderPass.ApplyPostEffectTemporal + getTemporalPass() );

			if( UsingMotionVectors == true )
			{
				AmplifyOcclusionCommon.SafeReleaseTemporaryRT( cb, tmpMotionIntensityRT );
			}
		}
		else
		{
			cb.SetGlobalTexture( PropertyID._AO_CurrOcclusionDepth, m_occlusionDepthRT );
			PerformBlit( cb, aDestinyRT, m_applyOcclusionMat, ShaderPass.ApplyPostEffect );
		}

		cb.SetRenderTarget( default( RenderTexture ) );
		cb.EndSample( "AO 3 - ApplyPostEffect" );
	}


	void commandBuffer_FillApplyDebug( CommandBuffer cb, RenderTargetIdentifier aSourceRT, RenderTargetIdentifier aDestinyRT )
	{
		cb.BeginSample( "AO 3 - ApplyDebug" );

		cb.SetGlobalTexture( PropertyID._MainTex, aSourceRT );

		if( UsingTemporalFilter )
		{
			commandBuffer_TemporalFilter( cb );

			int tmpMotionIntensityRT = 0;

			if( UsingMotionVectors == true )
			{
				tmpMotionIntensityRT = commandBuffer_NeighborMotionIntensity( cb, m_target.width, m_target.height );
			}

			applyDebugTargetsTemporal[0] = aDestinyRT;
			applyDebugTargetsTemporal[1] = new RenderTargetIdentifier( m_temporalAccumRT[ m_curStepIdx ] );

			cb.SetRenderTarget( applyDebugTargetsTemporal, applyDebugTargetsTemporal[ 0 ] /* Not used, just to make Unity happy */ );
			PerformBlit( cb, m_applyOcclusionMat, ShaderPass.ApplyDebugTemporal + getTemporalPass() );

			if( UsingMotionVectors == true )
			{
				AmplifyOcclusionCommon.SafeReleaseTemporaryRT( cb, tmpMotionIntensityRT );
			}
		}
		else
		{
			cb.SetGlobalTexture( PropertyID._AO_CurrOcclusionDepth, m_occlusionDepthRT );
			PerformBlit( cb, aDestinyRT, m_applyOcclusionMat, ShaderPass.ApplyDebug );
		}

		cb.SetRenderTarget( default( RenderTexture ) );
		cb.EndSample( "AO 3 - ApplyDebug" );
	}


	// Current state variables
	private bool m_HDR = true;
	private bool m_MSAA = true;

	// Previous state variables
	private SampleCountLevel m_prevSampleCount = SampleCountLevel.Low;
	private bool m_prevDownsample = false;
	private bool m_prevCacheAware = false;
	private bool m_prevBlurEnabled = false;
	private int m_prevBlurRadius = 0;
	private int m_prevBlurPasses = 0;
	private bool m_prevFilterEnabled = true;
	private bool m_prevHDR = true;
	private bool m_prevMSAA = true;

	private RenderTargetIdentifier[] applyDebugTargetsTemporal = new RenderTargetIdentifier[2];
	private RenderTargetIdentifier[] applyPostEffectTargetsTemporal = new RenderTargetIdentifier[2];
	
	private bool UsingTemporalFilter { get { return (settings.FilterEnabled == true); } }
	private bool UsingMotionVectors = false;
	private bool m_paramsChanged = true;
	private bool m_clearHistory = true;

	private void clearHistory( CommandBuffer cb )
	{
		m_clearHistory = false;

		if ( ( m_temporalAccumRT != null ) && ( m_occlusionDepthRT != null ) )
		{
			cb.SetGlobalTexture( PropertyID._AO_CurrOcclusionDepth, m_occlusionDepthRT );
			cb.SetRenderTarget( m_temporalAccumRT[ 0 ] );
			PerformBlit( cb, m_occlusionMat, ShaderPass.ClearTemporal );

			cb.SetGlobalTexture( PropertyID._AO_CurrOcclusionDepth, m_occlusionDepthRT );
			cb.SetRenderTarget( m_temporalAccumRT[ 1 ] );
			PerformBlit( cb, m_occlusionMat, ShaderPass.ClearTemporal );
		}
	}


	private void checkParamsChanged( Camera aCamera )
	{
		bool HDR = aCamera.allowHDR; // && tier?
		bool MSAA = aCamera.allowMSAA &&
					QualitySettings.antiAliasing >= 1;

		int antiAliasing = MSAA ? QualitySettings.antiAliasing : 1;
		//int antiAliasing = 1;

		if( m_occlusionDepthRT != null )
		{
			if( ( m_occlusionDepthRT.width != m_target.width ) ||
				( m_occlusionDepthRT.height != m_target.height ) ||
				( m_prevMSAA != MSAA ) ||
				( m_prevFilterEnabled != UsingTemporalFilter ) ||
				( !m_occlusionDepthRT.IsCreated() ) ||
				( m_temporalAccumRT != null && ( !m_temporalAccumRT[ 0 ].IsCreated() || !m_temporalAccumRT[ 1 ].IsCreated() ) )
				)
			{
				AmplifyOcclusionCommon.SafeReleaseRT( ref m_occlusionDepthRT );
				AmplifyOcclusionCommon.SafeReleaseRT( ref m_depthMipmap );
				releaseTemporalRT();

				m_paramsChanged = true;
			}
		}

		if( m_temporalAccumRT != null )
		{
			if( m_temporalAccumRT.Length != 2 )
			{
				m_temporalAccumRT = null;
			}
		}

		if( m_occlusionDepthRT == null )
		{
			m_occlusionDepthRT = AmplifyOcclusionCommon.SafeAllocateRT( "_AO_OcclusionDepthTexture",
																		m_target.width,
																		m_target.height,
																		m_occlusionRTFormat,
																		RenderTextureReadWrite.Linear,
																		FilterMode.Bilinear );
		}


		if( m_temporalAccumRT == null && UsingTemporalFilter )
		{
			m_temporalAccumRT = new RenderTexture[ 2 ];

			m_temporalAccumRT[ 0 ] = AmplifyOcclusionCommon.SafeAllocateRT( "_AO_TemporalAccum_0",
																			m_target.width,
																			m_target.height,
																			m_accumTemporalRTFormat,
																			RenderTextureReadWrite.Linear,
																			FilterMode.Bilinear,
																			antiAliasing );

			m_temporalAccumRT[ 1 ] = AmplifyOcclusionCommon.SafeAllocateRT( "_AO_TemporalAccum_1",
																			m_target.width,
																			m_target.height,
																			m_accumTemporalRTFormat,
																			RenderTextureReadWrite.Linear,
																			FilterMode.Bilinear,
																			antiAliasing );

			m_clearHistory = true;
		}

		if( ( settings.CacheAware == true ) && ( m_depthMipmap == null ) )
		{
			m_depthMipmap = AmplifyOcclusionCommon.SafeAllocateRT( "_AO_DepthMipmap",
																	m_target.width >> 1,
																	m_target.height >> 1,
																	RenderTextureFormat.RFloat,
																	RenderTextureReadWrite.Linear,
																	FilterMode.Point,
																	1,
																	true );

			int minSize = (int)Mathf.Min( m_target.width, m_target.height );
			m_numberMips = (int)( Mathf.Log( (float)minSize, 2.0f ) + 1.0f ) - 1;

			m_tmpMipString = null;
			m_tmpMipString = new string[m_numberMips];

			for( int i = 0; i < m_numberMips; i++ )
			{
				m_tmpMipString[i] = "_AO_TmpMip_" + i.ToString();
			}
		}
		else
		{
			if( ( settings.CacheAware == false ) && ( m_depthMipmap != null ) )
			{
				AmplifyOcclusionCommon.SafeReleaseRT( ref m_depthMipmap );
				m_tmpMipString = null;
			}
		}

		if( ( m_prevSampleCount != settings.SampleCount ) ||
			( m_prevDownsample != settings.Downsample ) ||
			( m_prevCacheAware != settings.CacheAware ) ||
			( m_prevBlurEnabled != settings.BlurEnabled ) ||
			( m_prevBlurPasses != settings.BlurPasses ) ||
			( m_prevBlurRadius != settings.BlurRadius ) ||
			( m_prevFilterEnabled != UsingTemporalFilter ) ||
			( m_prevHDR != HDR ) ||
			( m_prevMSAA != MSAA ) )
		{
			m_clearHistory |= ( m_prevHDR != HDR );
			m_clearHistory |= ( m_prevMSAA != MSAA );

			m_HDR = HDR;
			m_MSAA = MSAA;

			m_paramsChanged = true;
		}
	}


	private void updateParams()
	{
		m_prevSampleCount = settings.SampleCount;
		m_prevDownsample = settings.Downsample;
		m_prevCacheAware = settings.CacheAware;
		m_prevBlurEnabled = settings.BlurEnabled;
		m_prevBlurPasses = settings.BlurPasses;
		m_prevBlurRadius = settings.BlurRadius;
		m_prevFilterEnabled = UsingTemporalFilter;
		m_prevHDR = m_HDR;
		m_prevMSAA = m_MSAA;

		m_paramsChanged = false;
	}


	public override DepthTextureMode GetCameraFlags()
	{
		return DepthTextureMode.Depth | DepthTextureMode.MotionVectors;
	}


	private UnityEngine.Experimental.Rendering.HDPipeline.HDRenderPipeline m_hdRender = null;

	public override void Init()
	{
		if( !checkRenderTextureFormats() )
		{
			UnityEngine.Debug.LogError( "[AmplifyOcclusion] Target platform does not meet the minimum requirements for this effect to work properly." );

			this.settings.enabled = new BoolParameter{ value = false };

			return;
		}

		checkMaterials( false );

		// Check for MotionVectors support
		m_hdRender = UnityEngine.Experimental.Rendering.RenderPipelineManager.currentPipeline as UnityEngine.Experimental.Rendering.HDPipeline.HDRenderPipeline;

		if( ( m_hdRender != null ) &&
			// Due to a LW SRP bug this is not currently set for Build targets but MotionVectors only exist in the HD pipeline.
			( UnityEngine.Experimental.Rendering.SupportedRenderingFeatures.active.rendererSupportsMotionVectors == true ) &&
			( SystemInfo.supportsMotionVectors ) )
		{
			// Checks the HD Rendering Pipeline Asset -> "Render Pipeline Settings" -> "Support Motion Vectors"
			if( m_hdRender.renderPipelineSettings.supportMotionVectors == true )
			{
				UsingMotionVectors = true;
				// !TODO: There is also per frame camera motionvector settings that currently Unity SRP is not passing to the 
				// context.camera
			}
		}
	}


	public override void Release()
	{
		AmplifyOcclusionCommon.SafeReleaseRT( ref m_occlusionDepthRT );
		AmplifyOcclusionCommon.SafeReleaseRT( ref m_depthMipmap );
		releaseTemporalRT();
	}


	public override void Render( PostProcessRenderContext context )
	{
		if( m_hdRender == null )
		{
			if( settings.PerPixelNormals != PerPixelNormalSource.None )
			{
				settings.PerPixelNormals = new PerPixelNormalSourceParameter{ value = PerPixelNormalSource.None };

				UnityEngine.Debug.LogWarning( "[AmplifyOcclusion] GBuffer Normals only available in HDSRP Camera on Deferred Shading mode. Switched to Depth based normals." );
			}
		}
		else
		{
			if( ( settings.PerPixelNormals != PerPixelNormalSource.None ) &&
				( m_hdRender.renderPipelineSettings.supportedLitShaderMode == UnityEngine.Experimental.Rendering.HDPipeline.RenderPipelineSettings.SupportedLitShaderMode.ForwardOnly ) )
			{
				settings.PerPixelNormals = new PerPixelNormalSourceParameter{ value = PerPixelNormalSource.None };

				UnityEngine.Debug.LogWarning( "[AmplifyOcclusion] GBuffer Normals only available in HDSRP Camera on Deferred Shading mode. Switched to Depth based normals." );
			}
		}
		
		var cmd = context.command;
		cmd.BeginSample( "AO - Render" );

		checkMaterials( true );

		m_curStepIdx = m_sampleStep & 1;

		UpdateGlobalShaderConstants( cmd, context.camera );

		checkParamsChanged( context.camera );

		UpdateGlobalShaderConstants_AmbientOcclusion( cmd );
		UpdateGlobalShaderConstants_Matrices( cmd, context.camera );

		commandBuffer_FillComputeOcclusion( cmd );

		if( settings.ApplyMethod == ApplicationMethod.Debug )
		{
			commandBuffer_FillApplyDebug( cmd, context.source, context.destination );
		}
		else
		{
			commandBuffer_FillApplyPostEffect( cmd, context.source, context.destination );
		}

		updateParams();

		m_sampleStep++; // No clamp, free running counter

		cmd.EndSample( "AO - Render" );
	}


	private void releaseTemporalRT()
	{
		if( m_temporalAccumRT != null )
		{
			if( m_temporalAccumRT.Length != 0 )
			{
				AmplifyOcclusionCommon.SafeReleaseRT( ref m_temporalAccumRT[ 0 ] );
				AmplifyOcclusionCommon.SafeReleaseRT( ref m_temporalAccumRT[ 1 ] );
			}
		}

		m_temporalAccumRT = null;
	}
}
