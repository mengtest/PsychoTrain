// 
// HyperText.cs
// 
// Copyright (c) 2014-2016, Candlelight Interactive, LLC
// All rights reserved.
// 
// This file is licensed according to the terms of the Unity Asset Store EULA:
// http://download.unity3d.com/assetstore/customer-eula.pdf

#if UNITY_4_6_4 || UNITY_5_0
#define IS_TEXTGEN_SCALE_FACTOR_ABSENT
#endif
#if UNITY_4_6 || UNITY_4_7 || UNITY_5_0 || UNITY_5_1
#define IS_VBO_UI_VERTEX
#else
#define IS_VBO_MESH
#endif

using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Candlelight.UI
{
	/// <summary>
	/// Different color tint modes.
	/// </summary>
	public enum ColorTintMode
	{
		/// <summary>
		/// The color should be multiplied over the top of the underlying color.
		/// </summary>
		Multiplicative,
		/// <summary>
		/// The color should replace the underlying color.
		/// </summary>
		Constant,
		/// <summary>
		/// The color should be added to the underlying color.
		/// </summary>
		Additive
	}

	/// <summary>
	/// A <see cref="UnityEngine.UI.Text"/> component which can contain links and apply custom styles.
	/// </summary>
	/// <remarks>
	/// <para>This component internally uses a <see cref="HyperTextProcessor"/> to support &lt;a&gt; tags and custom
	/// styling with user-defined tags and &lt;quad&gt; classes. See <see cref="HyperTextProcessor"/> for information on 
	/// syntax, as well as automatic detection and tagging of keywords.</para>
	/// <para>Links extracted by the <see cref="HyperTextProcessor"/> are then colorized and emit callbacks for
	/// different pointer events. For example, the text <c>"Here is a &lt;a name="some_link"&gt;link&lt;/a&gt;"</c> will
	/// render as <c>Here is a link</c>, but with coloration and mouseover events for events for the word <c>link</c>.
	/// When the word <c>link</c> is clicked, entered, or exited, the component will emit callbacks of type
	/// <see cref="HyperText.HyperlinkEvent"/> specifying that a link with the id <c>"some_link"</c> was involved, along 
	/// with its hit boxes.</para>
	/// </remarks>
	[AddComponentMenu("UI/Candlelight/HyperText"), ExecuteInEditMode]
	public class HyperText : TextEx,
		UnityEngine.EventSystems.IPointerClickHandler,
		UnityEngine.EventSystems.IPointerDownHandler,
		UnityEngine.EventSystems.IPointerEnterHandler,
		UnityEngine.EventSystems.IPointerExitHandler,
		UnityEngine.EventSystems.IPointerUpHandler
	{
		/// <summary>
		/// Possible link selection states.
		/// </summary>
		internal enum LinkSelectionState
		{
			/// <summary>
			/// Default state.
			/// </summary>
			Normal,
			/// <summary>
			/// State when a link is selected or under the cursor.
			/// </summary>
			Highlighted,
			/// <summary>
			/// State when a link is pressed.
			/// </summary>
			Pressed,
			/// <summary>
			/// State when a link is disabled.
			/// </summary>
			Disabled
		}

		/// <summary>
		/// A class for storing information about a link indicated in the text.
		/// </summary>
		private class Link : TagGeometryData
		{
			/// <summary>
			/// The hitboxes for the link.
			/// </summary>
			private List<Rect> m_Hitboxes = new List<Rect>(1);

			/// <summary>
			/// Gets the name of the class.
			/// </summary>
			/// <value>The name of the class.</value>
			public string ClassName { get; private set; }
			/// <summary>
			/// Gets or sets the color tween runner.
			/// </summary>
			/// <value>The color tween runner.</value>
			public ColorTween.Runner ColorTweenRunner { get; private set; }
			/// <summary>
			/// Gets the index of the this instance in the <see cref="HyperText"/> instance where it occurs.
			/// </summary>
			/// <value>The index of the this instance in the <see cref="HyperText"/> instance where it occurs.</value>
			public int Index { get; private set; }
			/// <summary>
			/// Gets the <see cref="LinkInfo"/> for this instance.
			/// </summary>
			/// <value>The <see cref="LinkInfo"/> for this instance.</value>
			public LinkInfo Info { get { return new LinkInfo(this.Index, this.Name, this.ClassName); } }
			/// <summary>
			/// Gets the value of the <c>name</c> attribute.
			/// </summary>
			/// <value>The value of the <c>name</c> attribute.</value>
			public string Name { get; private set; }
			/// <summary>
			/// Gets or sets the style.
			/// </summary>
			/// <value>The style.</value>
			public HyperTextStyles.Link Style { get; private set; }
			/// <summary>
			/// Gets or sets the tint color.
			/// </summary>
			/// <value>The tint color.</value>
			public Color32 Tint { get; set; }
			/// <summary>
			/// Gets the vertical offset as a percentage of the surrounding line height.
			/// </summary>
			/// <value>The vertical offset as a percentage of the surrounding line height.</value>
			protected override float VerticalOffset { get { return this.Style.VerticalOffset; } }

			/// <summary>
			/// Initializes a new instance of the <see cref="HyperText.Link"/> class.
			/// </summary>
			/// <param name="index">Index of the link in the <see cref="HyperText"/> instance.</param>
			/// <param name="data">Data from a <see cref="HyperTextProcessor"/>.</param>
			/// <param name="hyperText">Hyper text.</param>
			public Link(int index, HyperTextProcessor.Link data, HyperText hyperText) : base(data.CharacterIndices)
			{
				this.Index = index;
				this.Name = data.Name;
				this.ClassName = data.ClassName;
				this.ColorTweenRunner = new ColorTween.Runner(hyperText);
				this.Style = data.Style;
				this.Tint = hyperText.GetTargetLinkTintForState(
					hyperText.IsInteractable() ? LinkSelectionState.Normal : LinkSelectionState.Disabled, this.Style
				);
			}

			/// <summary>
			/// Tests whether this instance contains the specified position in the space of this instance.
			/// </summary>
			/// <param name="uiPosition">Position in the space of this instance.</param>
			public bool Contains(Vector2 uiPosition)
			{
				for (int i = 0; i < m_Hitboxes.Count; ++i)
				{
					if (m_Hitboxes[i].Contains(uiPosition))
					{
						return true;
					}
				}
				return false;
			}

			/// <summary>
			/// Gets the hitboxes.
			/// </summary>
			/// <param name="hitboxes">A list of <see cref="UnityEngine.Rect"/>s to populate.</param>
			public void GetHitboxes(ref List<Rect> hitboxes)
			{
				hitboxes = hitboxes ?? new List<Rect>();
				hitboxes.Clear();
				hitboxes.AddRange(m_Hitboxes);
			}

			/// <summary>
			/// Sets the hitboxes.
			/// </summary>
			/// <param name="value">Value.</param>
			public void SetHitboxes(IEnumerable<Rect> value)
			{
				value = value ?? new Rect[0];
				m_Hitboxes.Clear();
				m_Hitboxes.AddRange(value);
			}
		}

		/// <summary>
		/// A structure with minimal information about a link involved in an event.
		/// </summary>
		public struct LinkInfo
		{
			/// <summary>
			/// Gets the name of the style class for the link, if any.
			/// </summary>
			/// <value>The name of the style class for the link, if any.</value>
			public string ClassName { get; private set; }
			/// <summary>
			/// The index of the link in the <see cref="HyperText"/> instance.
			/// </summary>
			/// <value>The index of the link in the <see cref="HyperText"/> instance.</value>
			public int Index { get; private set; }
			/// <summary>
			/// Gets the value of the link's <c>name</c> attribute.
			/// </summary>
			/// <value>The value of the link's <c>name</c> attribute.</value>
			public string Name { get; private set; }

			/// <summary>
			/// Initializes a new instance of the <see cref="LinkInfo"/> struct.
			/// </summary>
			/// <param name="index">Index of the link in the <see cref="HyperText"/> instance.</param>
			/// <param name="linkName">Value of the link's <c>name</c> attribute.</param>
			/// <param name="className">Value of the link's <c>class</c> attribute, if any.</param>
			public LinkInfo(int index, string linkName, string className) : this()
			{
				this.ClassName = className;
				this.Name = linkName;
				this.Index = index;
			}

			#region Obsolete
			[System.Obsolete("Use HyperText.GetLinkHitboxes(int, ref List<Rect>)", true)]
			public Rect[] Hitboxes { get { return null; } }
			[System.Obsolete("Use LinkInfo.Name")]
			public string Id { get { return this.Name; } }
			/// <summary>
			/// This constructor is obsolete. Use
			/// <see cref="M:Candlelight.UI.HyperText.LinkInfo.#ctor(System.Int32,System.String,System.String)" />
			/// instead.
			/// </summary>
			/// <param name="id">Value of the link's <c>name</c> attribute.</param>
			/// <param name="className">Value of the link's <c>class</c> attribute, if any.</param>
			/// <param name="hitboxes">This parameter is obsolete.</param>
			[System.Obsolete("Use LinkInfo(int, string, string)", true)]
			public LinkInfo(string id, string className, Rect[] hitboxes) : this(-1, id, className) {}
			#endregion
		}

		/// <summary>
		/// A class for storing information about a custom tag indicated in the text.
		/// </summary>
		private class CustomTag : TagGeometryData
		{
			/// <summary>
			/// Gets the style.
			/// </summary>
			/// <value>The style.</value>
			public HyperTextStyles.Text Style { get; private set; }
			/// <summary>
			/// Gets the vertical offset as a percentage of the surrounding line height.
			/// </summary>
			/// <value>The vertical offset as a percentage of the surrounding line height.</value>
			protected override float VerticalOffset { get { return this.Style.VerticalOffset; } }

			/// <summary>
			/// Initializes a new instance of the <see cref="HyperText.CustomTag"/> class.
			/// </summary>
			/// <param name="data">Data from a <see cref="Candlelight.UI.HyperTextProcessor"/>.</param>
			public CustomTag(HyperTextProcessor.CustomTag data) : base(data.CharacterIndices)
			{
				this.Style = data.Style;
			}
		}

		/// <summary>
		/// A class for storing information about a quad indicated in the text.
		/// </summary>
		private class Quad : TagGeometryData
		{
			/// <summary>
			/// Gets or sets the renderer.
			/// </summary>
			/// <value>The renderer.</value>
			public CanvasRenderer Renderer { get; set; }
			/// <summary>
			/// Gets the <see cref="UnityEngine.RectTransform"/>.
			/// </summary>
			/// <value>The <see cref="UnityEngine.RectTransform"/>.</value>
			public RectTransform RectTransform
			{
				get { return this.Renderer == null ? null : this.Renderer.transform as RectTransform; }
			}
			/// <summary>
			/// Gets the style.
			/// </summary>
			/// <value>The style.</value>
			public HyperTextStyles.Quad Style { get; private set; }
			/// <summary>
			/// Gets the texture.
			/// </summary>
			/// <value>The texture.</value>
			public Texture2D Texture { get { return this.Style.Sprite == null ? null : this.Style.Sprite.texture; } }
			/// <summary>
			/// Gets the UV rectangle for the sprite.
			/// </summary>
			/// <value>The UV rectangle for the sprite.</value>
			public Rect UVRect
			{
				get
				{
					if (this.Style.Sprite == null)
					{
						return new Rect(0f, 0f, 1f, 1f);
					}
					Vector4 v = UnityEngine.Sprites.DataUtility.GetOuterUV(this.Style.Sprite);
					return new Rect(v.x, v.y, v.z - v.x, v.w - v.y);
				}
			}
			/// <summary>
			/// Gets the vertical offset as a percentage of the surrounding line height.
			/// </summary>
			/// <value>The vertical offset as a percentage of the surrounding line height.</value>
			protected override float VerticalOffset { get { return this.Style.VerticalOffset; } }

			/// <summary>
			/// Initializes a new instance of the <see cref="HyperText.Quad"/> class.
			/// </summary>
			/// <param name="data">Data from a <see cref="HyperTextProcessor"/>.</param>
			public Quad(HyperTextProcessor.Quad data) : base(data.CharacterIndices)
			{
				this.Style = data.Style;
			}
		}

		/// <summary>
		/// A base class for storing data about the geometry for a tag appearing in the text.
		/// </summary>
		private abstract class TagGeometryData
		{
			/// <summary>
			/// Gets the list of indices for vertices that are redrawn as a consequence of
			/// <see cref="UnityEngine.UI.MeshModifier"/> effects.
			/// </summary>
			/// <value>The list of redraw indices.</value>
			public List<IndexRange> RedrawVertexIndices { get; private set; }
			/// <summary>
			/// Gets or sets the vertex indices.
			/// </summary>
			/// <value>The vertex indices.</value>
			public IndexRange VertexIndices { get; private set; }
			/// <summary>
			/// Gets the vertical offset as a percentage of the surrounding line height.
			/// </summary>
			/// <value>The vertical offset as a percentage of the surrounding line height.</value>
			protected abstract float VerticalOffset { get; }

			/// <summary>
			/// Gets the vertical offset.
			/// </summary>
			/// <returns>The vertical offset.</returns>
			/// <param name="fontSize">Font size.</param>
			public float GetVerticalOffset(int fontSize)
			{
				return this.VerticalOffset * fontSize;
			}

			/// <summary>
			/// Initializes a new instance of the <see cref="HyperText.TagGeometryData"/> class.
			/// </summary>
			/// <param name="characterIndices">Character indices.</param>
			public TagGeometryData(IndexRange characterIndices)
			{
				this.VertexIndices = new IndexRange(characterIndices.StartIndex * 4, characterIndices.EndIndex * 4 + 3);
				this.RedrawVertexIndices = new List<IndexRange>();
			}
		}

		/// <summary>
		/// An event class for handling hyperlinks.
		/// </summary>
		[System.Serializable]
		public class HyperlinkEvent : UnityEngine.Events.UnityEvent<HyperText, LinkInfo> {}
			
#pragma warning disable 414
		/// <summary>
		/// A flag to specify whether the graphics device is known yet.
		/// </summary>
		private static bool s_IsGraphicsDeviceKnown = false;
		/// <summary>
		/// The maximum number of materials on a <see cref="UnityEngine.CanvasRenderer"/>.
		/// </summary>
		private static readonly int s_MaxCanvasRendererMaterials = 5;
		/// <summary>
		/// The property identifier for the texture slot on quad materials.
		/// </summary>
		private static int s_QuadTextureId;
#pragma warning restore 414
		/// <summary>
		/// The color of untinted vertices.
		/// </summary>
		private static readonly Color32 s_UntintedVertexColor = Color.white;
		#region Shared Allocations
#pragma warning disable 414
#if IS_VBO_MESH
		private static readonly UIVertex[] s_GlyphVertices = new UIVertex[4];
	#if !(UNITY_5_2_0 || UNITY_5_2_1)
		private static readonly UnityEngine.UI.VertexHelper s_VertexHelper = new UnityEngine.UI.VertexHelper();
	#endif
#else
		private static Material s_StencilTrigger;
#endif
		private static readonly Vector2[] s_UVTransform = new Vector2[4];
#pragma warning restore 414
		#endregion

		#region Backing Fields
		private static bool s_ShouldSwizzleQuadRedBlue;
		#endregion

		/// <summary>
		/// Gets a value indicating whether the red and blue channels of quad vertex colors need to be swapped.
		/// </summary>
		/// <remarks>TextGenerator swaps these channels on DX9 and lower.</remarks>
		/// <value>
		/// <see langword="true"/> if the red and blue channels of quad vertex colors need to be swapped; otherwise,
		/// <see langword="false"/>.
		/// </value>
		private static bool ShouldSwizzleQuadRedBlue
		{
			get
			{
				if (!s_IsGraphicsDeviceKnown)
				{
					s_ShouldSwizzleQuadRedBlue =
						SystemInfo.graphicsDeviceVersion.StartsWith("Direct3D") &&
						SystemInfo.graphicsShaderLevel <= 30 &&
						SystemInfo.graphicsDeviceVersion != "Direct3D 9.0c [emulated]"; // bug won't appear with emulation
					s_IsGraphicsDeviceKnown = true;
				}
				return s_ShouldSwizzleQuadRedBlue;
			}
		}

		#region Backing Fields
		private static Material s_DefaultQuadMaterial = null;
		#endregion

		/// <summary>
		/// A flag to specify whether or not vertices are dirty.
		/// </summary>
		private bool m_AreVerticesDirty = true;
		/// <summary>
		/// The base vertex colors before any link effects are applied.
		/// </summary>
		private readonly List<Color32> m_BaseVertexColors = new List<Color32>();
		/// <summary>
		/// The custom tag geometry data extracted from the text.
		/// </summary>
		private readonly List<CustomTag> m_CustomTagGeometryData = new List<CustomTag>();
		/// <summary>
		/// The default styles to use when a new component is added.
		/// </summary>
		[SerializeField, HideInInspector]
		private HyperTextStyles m_DefaultStyles = null;
		/// <summary>
		/// A flag to indicate whether the font texture changed callback should be invoked.
		/// </summary>
		private bool m_DisableFontTextureChangedCallback = false;
		/// <summary>
		/// A flag to keep track of whether interactability is permitted by any canvas groups.
		/// </summary>
		private bool m_DoGroupsAllowInteraction = true;
		/// <summary>
		/// An allocation;
		/// </summary>
		private Link m_HitboxCandidate = null;
		/// <summary>
		/// The hit box under the cursor when the pointer down event is raised.
		/// </summary>
		private Link m_HitboxOnPointerDown = null;
		/// <summary>
		/// The hit box under the cursor.
		/// </summary>
		private Link m_HitboxUnderCursor = null;
		/// <summary>
		/// A flag to indicating whether or not link colors need to be updated.
		/// </summary>
		private bool m_IsAnimatingLinkStateTransition = false;
		/// <summary>
		/// The link geometry data extracted from the text.
		/// </summary>
		private readonly List<Link> m_LinkGeometryData = new List<Link>();
		/// <summary>
		/// The most recent enter event camera.
		/// </summary>
		private Camera m_MostRecentEnterEventCamera;
		/// <summary>
		/// The quad material after the application of masking.
		/// </summary>
		protected Material m_QuadMaskMaterial = null;
#pragma warning disable 649
		/// <summary>
		/// The quad material to use on the CanvasRenderer.
		/// </summary>
		private Material m_QuadMaterialForRendering = null;
#pragma warning restore 649
		/// <summary>
		/// The quad geometry data extracted from the text.
		/// </summary>
		private readonly List<Quad> m_QuadGeometryData = new List<Quad>();
#if IS_VBO_MESH
		/// <summary>
		/// A table of quad materials keyed by the texture used by the quad.
		/// </summary>
		private readonly Dictionary<Texture2D, Material> m_QuadMaterials = new Dictionary<Texture2D, Material>();
		/// <summary>
		/// A pool of meshes to use for the quads.
		/// </summary>
		private readonly List<Mesh> m_QuadMeshes = new List<Mesh>(8);
#endif
		/// <summary>
		/// The renderers for the quads.
		/// </summary>
		[SerializeField]
		private List<CanvasRenderer> m_QuadRenderersPool = new List<CanvasRenderer>();
#if IS_VBO_MESH
		/// <summary>
		/// The unique textures used by quads.
		/// </summary>
		private readonly List<Texture2D> m_QuadTextures = new List<Texture2D>(16);
#endif
		/// <summary>
		/// The quad tracker.
		/// </summary>
		private DrivenRectTransformTracker m_QuadTracker = new DrivenRectTransformTracker();
		/// <summary>
		/// A flag indicating whether or not the external dependency callback should be invoked. Used to prevent dirtying
		/// during rebuild phase.
		/// </summary>
		private bool m_ShouldInvokeExternalDependencyCallback = true;
		/// <summary>
		/// The postprocessed string most recently sent to the TextGenerator.
		/// </summary>
		private string m_TextGeneratorInput = null;
		/// <summary>
		/// The UIVertices.
		/// </summary>
		private readonly List<UIVertex> m_UIVertices = new List<UIVertex>();
#if IS_VBO_MESH
		/// <summary>
		/// The vertex colors.
		/// </summary>
		private readonly List<Color32> m_VertexColors = new List<Color32>();
#endif
		/// <summary>
		/// A cache of all vertex positions. When using UIVertex VBO path, these positions are pre-degeneration.
		/// </summary>
		private readonly List<Vector3> m_VertexPositions = new List<Vector3>();

		#region Backing Fields
#if IS_VBO_MESH
		private Mesh m_GlyphMesh = null;
#endif
		[SerializeField, UnityEngine.Serialization.FormerlySerializedAs("m_AreLinksEnabled"), PropertyBackingField]
		private bool m_Interactable = true;
		[SerializeField, PropertyBackingField]
		private ImmutableRectOffset m_LinkHitboxPadding = new ImmutableRectOffset(0, 0, 0, 0);
		[SerializeField]
		private HyperlinkEvent m_OnClick = new HyperlinkEvent();
		[SerializeField]
		private HyperlinkEvent m_OnEnter = new HyperlinkEvent();
		[SerializeField]
		private HyperlinkEvent m_OnExit = new HyperlinkEvent();
		[SerializeField]
		private HyperlinkEvent m_OnPress = new HyperlinkEvent();
		[SerializeField]
		private HyperlinkEvent m_OnRelease = new HyperlinkEvent();
		[SerializeField, PropertyBackingField]
		private Material m_QuadMaterial = null;
		[SerializeField]
		private bool m_ShouldOverrideStylesFontStyle = false;
		[SerializeField]
		private bool m_ShouldOverrideStylesFontColor = false;
		[SerializeField]
		private bool m_ShouldOverrideStylesLineSpacing = false;
		[SerializeField]
		private bool m_ShouldOverrideStylesLinkHitboxPadding = false;
		[SerializeField]
		private HyperTextProcessor m_TextProcessor = null;
		#endregion
		/// <summary>
		/// Gets the default link style.
		/// </summary>
		/// <value>The default link style.</value>
		public HyperTextStyles.Link DefaultLinkStyle
		{
			get
			{
				return this.Styles == null ? HyperTextStyles.Link.DefaultStyle : this.Styles.CascadedDefaultLinkStyle;
			}
		}
		/// <summary>
		/// Gets the default quad material.
		/// </summary>
		/// <value>The default quad material.</value>
		public virtual Material DefaultQuadMaterial
		{
			get
			{
				if (s_DefaultQuadMaterial == null)
				{
					s_DefaultQuadMaterial = defaultGraphicMaterial;
				}
				return s_DefaultQuadMaterial;
			}
		}
		/// <summary>
		/// Gets the default color of the text.
		/// </summary>
		/// <value>The default color of the text.</value>
		public Color DefaultTextColor
		{
			get
			{
				return this.Styles == null || m_ShouldOverrideStylesFontColor ?
					this.color : this.Styles.CascadedDefaultTextColor;
			}
		}
		/// <summary>
		/// Gets the default style of the text.
		/// </summary>
		/// <value>The default style of the text.</value>
		public FontStyle DefaultTextStyle
		{
			get
			{
				return this.Styles == null || m_ShouldOverrideStylesFontStyle ?
					this.fontStyle : this.Styles.CascadedDefaultFontStyle;
			}
		}
		/// <summary>
		/// Gets the font size to use.
		/// </summary>
		/// <value>The font size to use.</value>
		public int FontSizeToUse
		{
			get
			{
				return this.TextProcessor.ShouldOverrideStylesFontSize || this.Styles == null ?
					this.fontSize : this.Styles.CascadedFontSize;
			}
		}
		/// <summary>
		/// Gets the font to use.
		/// </summary>
		/// <value>The font to use.</value>
		public Font FontToUse
		{
			get { return this.font != null ? this.font : (this.Styles == null ? null : this.Styles.CascadedFont); }
		}
#if IS_VBO_MESH
		/// <summary>
		/// Gets the mesh to store the glyph geometry.
		/// </summary>
		/// <value>The glyph mesh.</value>
		private Mesh GlyphMesh
		{
			get
			{
				if (m_GlyphMesh == null)
				{
					m_GlyphMesh = new Mesh();
					m_GlyphMesh.hideFlags = HideFlags.HideAndDontSave;
				}
				return m_GlyphMesh;
			}
		}
#endif
		/// <summary>
		/// Gets or sets the input text source. If a value is assigned, its <see cref="ITextSource.OutputText"/> will be 
		/// used in place of the value in the <see cref="UnityEngine.UI.Text.text"/> property on this instance.
		/// </summary>
		/// <value>The input text source.</value>
		public ITextSource InputTextSource
		{
			get { return this.TextProcessor.InputTextSource; }
			set { this.TextProcessor.InputTextSource = value; }
		}
		/// <summary>
		/// Sets a value indicating whether links are interactable on this <see cref="HyperText"/>.
		/// </summary>
		/// <value><see langword="true"/> if links are interactable; otherwise, <see langword="false"/>.</value>
		public bool Interactable
		{
			get { return m_Interactable; }
			set
			{
				if (value == m_Interactable)
				{
					return;
				}
				m_Interactable = value;
				OnInteractableChanged();
			}
		}
		/// <summary>
		/// Gets a value indicating whether this instance is a prefab.
		/// </summary>
		/// <value><see langword="true"/> if this instance is a prefab; otherwise, <see langword="false"/>.</value>
		private bool IsPrefab
		{
			get
			{
#if UNITY_EDITOR
				return UnityEditor.PrefabUtility.GetPrefabType(this) == UnityEditor.PrefabType.Prefab;
#else
				return false;
#endif
			}
		}
		/// <summary>
		/// Gets a value indicating the number of units on each side that link hitboxes should extend beyond the bounds
		/// of the glyph geometry. Use positive values to generate link hitboxes that are larger than their encapsulated
		/// geometry (for, e.g., small screen devices).
		/// </summary>
		/// <value>
		/// The number of units on each side that link hitboxes should extend beyond the bounds of the glyph geometry.
		/// </value>
		public ImmutableRectOffset LinkHitboxPadding
		{
			get { return m_LinkHitboxPadding; }
			set
			{
				if (!m_LinkHitboxPadding.Equals(value))
				{
					m_LinkHitboxPadding = value;
					UpdateLinkHitboxRects();
				}
			}
		}
		/// <summary>
		/// Gets the main texture.
		/// </summary>
		/// <value>The main texture.</value>
		public override Texture mainTexture
		{
			get
			{
				if (
					this.FontToUse != null &&
					this.FontToUse.material != null &&
					this.FontToUse.material.mainTexture != null
				)
				{
					return this.FontToUse.material.mainTexture;
				}
				return m_Material != null ? m_Material.mainTexture : base.mainTexture;
			}
		}
		/// <summary>
		/// Gets the on click callback.
		/// </summary>
		/// <value>The on click callback.</value>
		public HyperlinkEvent OnClick { get { return m_OnClick; } }
		/// <summary>
		/// Gets the on enter callback.
		/// </summary>
		/// <value>The on enter callback.</value>
		public HyperlinkEvent OnEnter { get { return m_OnEnter; } }
		/// <summary>
		/// Gets the on exit callback.
		/// </summary>
		/// <value>The on exit callback.</value>
		public HyperlinkEvent OnExit { get { return m_OnExit; } }
		/// <summary>
		/// Gets the on press callback.
		/// </summary>
		/// <value>The on press callback.</value>
		public HyperlinkEvent OnPress { get { return m_OnPress; } }
		/// <summary>
		/// Gets the on release callback.
		/// </summary>
		/// <value>The on release callback.</value>
		public HyperlinkEvent OnRelease { get { return m_OnRelease; } }
		/// <summary>
		/// Gets the pixels per unit.
		/// </summary>
		/// <value>The pixels per unit.</value>
		new public float pixelsPerUnit
		{
			get
			{
				Canvas localCanvas = this.canvas;
				if (localCanvas == null)
				{
					return 1;
				}
				// For dynamic fonts, ensure we use one pixel per pixel on the screen.
				if (this.FontToUse == null || this.FontToUse.dynamic)
				{
					return localCanvas.scaleFactor;
				}
				// For non-dynamic fonts, calculate pixels per unit based on specified font size relative to font object's own font size.
				if (this.FontSizeToUse <= 0)
				{
					return 1;
				}
				return this.FontToUse.fontSize / (float)this.FontSizeToUse;
			}
		}
		/// <summary>
		/// Gets the preferred height for layout.
		/// </summary>
		/// <value>The preferred height for layout.</value>
		public override float preferredHeight
		{
			get
			{
				UpdateTextProcessor();
				return this.cachedTextGeneratorForLayout.GetPreferredHeight(
					this.TextProcessor.OutputText,
					GetGenerationSettings(new Vector2(this.rectTransform.rect.size.x, 0f))
				) / this.pixelsPerUnit;
			}
		}
		/// <summary>
		/// Gets the preferred width for layout.
		/// </summary>
		/// <value>The preferred width for layout.</value>
		public override float preferredWidth
		{
			get
			{
				UpdateTextProcessor();
                float tempWidth = this.cachedTextGeneratorForLayout.GetPreferredWidth(
					this.TextProcessor.OutputText,
					GetGenerationSettings(Vector2.zero)
				) / this.pixelsPerUnit;

                if (m_maxPreferredWidth == -1 || m_minPreferredWidth == -1)
                {
                    if (m_maxPreferredWidth != -1)
                        return Mathf.Min(m_maxPreferredWidth, tempWidth);
                    if (m_minPreferredWidth != -1)
                        return Mathf.Max(m_minPreferredWidth, tempWidth);

                    return tempWidth;
                }

                if (m_minPreferredWidth < m_maxPreferredWidth)
                    return Mathf.Clamp(tempWidth, m_minPreferredWidth, m_maxPreferredWidth);

                if (m_minPreferredWidth > m_maxPreferredWidth)
                    return Mathf.Clamp(tempWidth, m_maxPreferredWidth, m_minPreferredWidth);

                return m_minPreferredWidth;//等于的情况
            }
		}
		/// <summary>
		/// Gets the quad base material.
		/// </summary>
		/// <value>The quad base material.</value>
		private Material QuadBaseMaterial
		{
			get { return m_QuadMaterial == null ? this.DefaultQuadMaterial : m_QuadMaterial; }
		}
		/// <summary>
		/// Gets or sets the material to apply to quads.
		/// </summary>
		/// <value>The material to apply to quads.</value>
		public virtual Material QuadMaterial
		{
			get
			{
#if IS_VBO_UI_VERTEX
				// trigger stencil update (MaskableGraphic.UpdateInternalState())
				s_StencilTrigger = base.material;
				// return masked version if quads should be masked
				if (m_IncludeForMasking)
				{
					if (m_QuadMaskMaterial == null)
					{
						m_QuadMaskMaterial =
							UnityEngine.UI.StencilMaterial.Add(this.QuadBaseMaterial, (1 << m_StencilValue) - 1);
					}
					return m_QuadMaskMaterial ?? this.QuadBaseMaterial;
				}
				// otherwise return the result of the base material
#endif
				return this.QuadBaseMaterial;
			}
			set
			{
				if (m_QuadMaterial != value)
				{
					m_QuadMaterial = value;
					SetMaterialDirty();
				}
			}
		}
		/// <summary>
		/// Gets the quad material for rendering.
		/// </summary>
		/// <value>The quad material for rendering.</value>
		public virtual Material QuadMaterialForRendering
		{
			get { return m_QuadMaterialForRendering == null ? this.QuadMaterial : m_QuadMaterialForRendering; }
		}
		/// <summary>
		/// Gets or sets a value indicating whether this <see cref="HyperText"/> should override the font
		/// color specified in styles, if one is assigned.
		/// </summary>
		/// <value>
		/// <see langword="true"/> if should override the font color specified in styles; otherwise, 
		/// <see langword="false"/>.
		/// </value>
		public bool ShouldOverrideStylesFontColor
		{
			get { return m_ShouldOverrideStylesFontColor; }
			set
			{
				if (m_ShouldOverrideStylesFontColor != value)
				{
					m_ShouldOverrideStylesFontColor = value;
					SetAllDirty();
				}
			}
		}
		/// <summary>
		/// Gets or sets a value indicating whether this <see cref="HyperText"/> should override the font size specified
		/// in styles, if one is assigned.
		/// </summary>
		/// <value>
		/// <see langword="true"/> if should override the font size specified in styles; otherwise, 
		/// <see langword="false"/>.
		/// </value>
		public bool ShouldOverrideStylesFontSize
		{
			get { return this.TextProcessor.ShouldOverrideStylesFontSize; }
			set { this.TextProcessor.ShouldOverrideStylesFontSize = value; }
		}
		/// <summary>
		/// Gets or sets a value indicating whether this <see cref="HyperText"/> should override the font face specified
		/// in styles, if one is assigned.
		/// </summary>
		/// <value>
		/// <see langword="true"/> if the font face specified in styles should be overridden; otherwise, 
		/// <see langword="false"/>.
		/// </value>
		public bool ShouldOverrideStylesFontStyle
		{
			get { return m_ShouldOverrideStylesFontStyle; }
			set
			{
				if (m_ShouldOverrideStylesFontStyle != value)
				{
					m_ShouldOverrideStylesFontStyle = value;
					SetAllDirty();
				}
			}
		}
		/// <summary>
		/// Gets or sets a value indicating whether this instance should override the line spacing specified in styles
		/// if one is assigned.
		/// </summary>
		/// <value>
		/// <see langword="true"/> if the line spacing specified in styles should be overridden; otherwise,
		/// <see langword="false"/>.
		/// </value>
		public bool ShouldOverrideStylesLineSpacing
		{
			get { return m_ShouldOverrideStylesLineSpacing; }
			set
			{
				if (m_ShouldOverrideStylesLineSpacing != value)
				{
					m_ShouldOverrideStylesLineSpacing = value;
					SetAllDirty();
				}
			}
		}
		/// <summary>
		/// Gets or sets a value indicating whether this instance should override the link hitbox padding specified in
		/// styles if one is assigned.
		/// </summary>
		/// <value>
		/// <see langword="true"/> if the link hitbox padding specified in styles should be overriden; otherwise,
		/// <see langword="false"/>.
		/// </value>
		public bool ShouldOverrideLinkHitboxPadding
		{
			get { return m_ShouldOverrideStylesLinkHitboxPadding; }
			set
			{
				if (m_ShouldOverrideStylesLinkHitboxPadding != value)
				{
					m_ShouldOverrideStylesLinkHitboxPadding = value;
					UpdateLinkHitboxRects();
				}
			}
		}
		/// <summary>
		/// Gets or sets the styles.
		/// </summary>
		/// <value>The styles.</value>
		public HyperTextStyles Styles
		{
			get { return this.TextProcessor.Styles; }
			set { this.TextProcessor.Styles = value; }
		}
		/// <summary>
		/// Gets the text processor.
		/// </summary>
		/// <value>The text processor.</value>
		private HyperTextProcessor TextProcessor
		{
			get
			{
				if (m_TextProcessor == null)
				{
					m_TextProcessor = new HyperTextProcessor();
					m_TextProcessor.OnBecameDirty += OnTextProcessorBecameDirty;
					UpdateTextProcessor();
				}
				return m_TextProcessor;
			}
		}
		/// <summary>
		/// Gets the <see cref="System.String"/> uploaded to <see cref="UnityEngine.UI.Text.cachedTextGenerator"/>.
		/// </summary>
		/// <value>
		/// The <see cref="System.String"/> uploaded to <see cref="UnityEngine.UI.Text.cachedTextGenerator"/>.
		/// </value>
		public string UploadedText { get { return this.TextProcessor.OutputText; } }

		/// <summary>
		/// Clears the quad mask material.
		/// </summary>
		private void ClearQuadMaskMaterial()
		{
			if (m_QuadMaskMaterial != null)
			{
				UnityEngine.UI.StencilMaterial.Remove(m_QuadMaskMaterial);
			}
			m_QuadMaskMaterial = null;
		}

#if IS_VBO_MESH
		/// <summary>
		/// Cull this object's <see cref="UnityEngine.CanvasRenderer"/> if it is outside <paramref name="clipRect"/>.
		/// </summary>
		/// <param name="clipRect">Clipping rectangle.</param>
		/// <param name="validRect">
		/// If set to <see langword="true"/> then <paramref name="clipRect"/> is a valid rectangle.
		/// </param>
		public override void Cull(Rect clipRect, bool validRect)
		{
			base.Cull(clipRect, validRect);
			for (int i = 0; i < m_QuadRenderersPool.Count; ++i)
			{
				if (m_QuadRenderersPool[i] != null)
				{
					m_QuadRenderersPool[i].cull = this.canvasRenderer.cull;
				}
			}
		}
#endif
		
		/// <summary>
		/// Does a state transition for the specified link.
		/// </summary>
		/// <param name="link">Link.</param>
		/// <param name="newState">New state.</param>
		private void DoLinkStateTransition(Link link, LinkSelectionState newState)
		{
			if (!IsActive() || link == null)
			{
				return;
			}
			Color targetTint = GetTargetLinkTintForState(newState, link.Style);
			if (link.Tint == targetTint)
			{
				return;
			}
			ColorTween.Info colorTweenInfo = new ColorTween.Info(
				link.Style.Colors.fadeDuration, true, link.Tint, targetTint, link.Style.ColorTweenMode
			);
			colorTweenInfo.AddOnChangedCallback(value => link.Tint = value);
			colorTweenInfo.AddOnChangedCallback(value => m_IsAnimatingLinkStateTransition = true);
			link.ColorTweenRunner.StartTween(colorTweenInfo);
		}

		/// <summary>
		/// A callback to indicate the font texture has changed (mirrors that from base class).
		/// </summary>
		new public void FontTextureChanged()
		{
			if (Equals(null))
			{
				FontUpdateTracker.UntrackHyperText(this);
				return;
			}
			if (m_DisableFontTextureChangedCallback)
			{
				return;
			}
			this.cachedTextGenerator.Invalidate();
			if (!IsActive())
			{
				return;
			}
			if (
				UnityEngine.UI.CanvasUpdateRegistry.IsRebuildingGraphics() ||
				UnityEngine.UI.CanvasUpdateRegistry.IsRebuildingLayout()
			)
			{
				UpdateGeometry();
			}
			else
			{
				SetAllDirty();
			}
		}

		/// <summary>
		/// Gets the generation settings.
		/// </summary>
		/// <returns>The generation settings.</returns>
		/// <param name="extents">Extents.</param>
		new public TextGenerationSettings GetGenerationSettings(Vector2 extents)
		{
			TextGenerationSettings result = new TextGenerationSettings();
#if IS_TEXTGEN_SCALE_FACTOR_ABSENT
			result.generationExtents = extents * this.pixelsPerUnit + Vector2.one * 0.0001f; // Text.kEpsilon
			if (this.FontToUse != null && this.FontToUse.dynamic)
			{
				result.fontSize = Mathf.FloorToInt(this.FontSizeToUse * this.pixelsPerUnit);
				result.resizeTextMinSize = Mathf.FloorToInt(this.resizeTextMinSize * this.pixelsPerUnit);
				result.resizeTextMaxSize = Mathf.FloorToInt(this.resizeTextMaxSize * this.pixelsPerUnit);
			}
#else
			result.generationExtents = extents;
			if (this.FontToUse != null && this.FontToUse.dynamic)
			{
				result.fontSize = this.FontSizeToUse;
				result.resizeTextMinSize = this.resizeTextMinSize;
				result.resizeTextMaxSize = this.resizeTextMaxSize;
			}
			result.scaleFactor = this.pixelsPerUnit;
#endif
			result.textAnchor = this.alignment;
			result.color = this.DefaultTextColor;
			result.font = this.FontToUse;
			result.pivot = this.rectTransform.pivot;
			result.richText = this.supportRichText;
			result.lineSpacing = this.Styles == null || m_ShouldOverrideStylesLineSpacing ?
				this.lineSpacing : this.Styles.CascadedLineSpacing;
			result.fontStyle = this.DefaultTextStyle;
			result.resizeTextForBestFit = this.resizeTextForBestFit;
			result.updateBounds = false;
			result.horizontalOverflow = this.horizontalOverflow;
			result.verticalOverflow = this.verticalOverflow;
#if !UNITY_4_6 && !UNITY_4_7 && !UNITY_5_0 && !UNITY_5_1 && !UNITY_5_2
			result.alignByGeometry = this.alignByGeometry;
#endif
			return result;
		}

		/// <summary>
		/// Gets the link at the specified world position.
		/// </summary>
		/// <returns>The link at the specified world position.</returns>
		/// <param name="pointerPosition">Pointer position.</param>
		/// <param name="eventCamera">Event camera.</param>
		private Link GetLinkAtPointerPosition(Vector3 pointerPosition, Camera eventCamera)
		{
			if (eventCamera != null)
			{
				float distance;
				Ray ray = eventCamera.ScreenPointToRay(pointerPosition);
				if (!new Plane(-this.transform.forward, this.transform.position).Raycast(ray, out distance))
				{
					return null;
				}
				pointerPosition = ray.GetPoint(distance);
			}
			Vector3 uiPosition = this.transform.InverseTransformPoint(pointerPosition);
			for (int i = 0; i < m_LinkGeometryData.Count; ++i)
			{
				if (m_LinkGeometryData[i].Contains(uiPosition))
				{
					return m_LinkGeometryData[i];
				}
			}
			return null;
		}

		/// <summary>
		/// Gets the link hitboxes for the link with the specified index on this <see cref="HyperText"/>.
		/// </summary>
		/// <param name="linkIndex">Link index.</param>
		/// <param name="hitboxes">A list of <see cref="UnityEngine.Rect"/>s to populate.</param>
		public void GetLinkHitboxes(int linkIndex, ref List<Rect> hitboxes)
		{
			if (m_AreVerticesDirty)
			{
				UpdateGeometry();
			}
			hitboxes = hitboxes ?? new List<Rect>();
			hitboxes.Clear();
			if (linkIndex < m_LinkGeometryData.Count)
			{
				m_LinkGeometryData[linkIndex].GetHitboxes(ref hitboxes);
			}
		}

		/// <summary>
		/// Gets the link hitboxes.
		/// </summary>
		/// <param name="linkHitboxes">
		/// A dictionary to populate, mapping link information to local-space hit boxes.
		/// </param>
		public void GetLinkHitboxes(ref Dictionary<LinkInfo, List<Rect>> linkHitboxes)
		{
			if (m_AreVerticesDirty)
			{
				UpdateGeometry();
			}
			linkHitboxes = linkHitboxes ?? new Dictionary<LinkInfo, List<Rect>>();
			linkHitboxes.Clear();
			for (int i = 0; i < m_LinkGeometryData.Count; ++i)
			{
				List<Rect> hitboxes = null;
				m_LinkGeometryData[i].GetHitboxes(ref hitboxes);
				linkHitboxes.Add(m_LinkGeometryData[i].Info, hitboxes);
			}
		}

		/// <summary>
		/// Gets the link keyword collections.
		/// </summary>
		/// <param name="collections">Collections.</param>
		public void GetLinkKeywordCollections(ref List<HyperTextProcessor.KeywordCollectionClass> collections)
		{
			this.TextProcessor.GetLinkKeywordCollections(ref collections);
		}

#if IS_VBO_MESH
		/// <summary>
		/// Gets the modified material.
		/// </summary>
		/// <returns>The modified material.</returns>
		/// <param name="baseMaterial">Base material.</param>
		public override Material GetModifiedMaterial(Material baseMaterial)
		{
			Material result = base.GetModifiedMaterial(baseMaterial);
			// replicate logic in UnityEngine.UI.MaskableGraphic.GetModifiedMaterial()
			if (m_StencilValue > 0 && GetComponent<UnityEngine.UI.Mask>() == null)
			{
				if (m_QuadMaskMaterial != null)
				{
					UnityEngine.UI.StencilMaterial.Remove(m_QuadMaskMaterial);
				}
				m_QuadMaskMaterial = UnityEngine.UI.StencilMaterial.Add(
					this.QuadMaterial,
					(1 << m_StencilValue) - 1,
					UnityEngine.Rendering.StencilOp.Keep,
					UnityEngine.Rendering.CompareFunction.Equal,
					UnityEngine.Rendering.ColorWriteMask.All,
					(1 << m_StencilValue) - 1,
					0
				);
				m_QuadMaterialForRendering = m_QuadMaskMaterial;
			}
			else
			{
				m_QuadMaterialForRendering = this.QuadMaterial;
			}
			return result;
		}
#endif

		/// <summary>
		/// Gets the quad keyword collections.
		/// </summary>
		/// <param name="collections">Collections.</param>
		public void GetQuadKeywordCollections(ref List<HyperTextProcessor.KeywordCollectionClass> collections)
		{
			this.TextProcessor.GetQuadKeywordCollections(ref collections);
		}

		/// <summary>
		/// Gets the tag keyword collections.
		/// </summary>
		/// <param name="collections">Collections.</param>
		public void GetTagKeywordCollections(ref List<HyperTextProcessor.KeywordCollectionClass> collections)
		{
			this.TextProcessor.GetTagKeywordCollections(ref collections);
		}

		/// <summary>
		/// Gets the target link tint color for the specified state.
		/// </summary>
		/// <returns>The target link tint color for the specified state.</returns>
		/// <param name="state">A link select state.</param>
		/// <param name="style">The link style.</param>
		private Color GetTargetLinkTintForState(LinkSelectionState state, HyperTextStyles.Link style)
		{
			Color stateColor = style.Colors.normalColor;
			switch (state)
			{
			case LinkSelectionState.Disabled:
				stateColor = style.Colors.disabledColor;
				break;
			case LinkSelectionState.Highlighted:
				stateColor = style.Colors.highlightedColor;
				break;
			case LinkSelectionState.Normal:
				stateColor = style.Colors.normalColor;
				break;
			case LinkSelectionState.Pressed:
				stateColor = style.Colors.pressedColor;
				break;
			}
			return stateColor * style.Colors.colorMultiplier;
		}
		
		/// <summary>
		/// Determines whether this instance is interactable.
		/// </summary>
		/// <returns>
		/// <see langword="true"/> if this instance is interactable; otherwise, <see langword="false"/>.
		/// </returns>
		protected bool IsInteractable()
		{
			return m_DoGroupsAllowInteraction && m_Interactable;
		}

		/// <summary>
		/// Raises the canvas group changed event. Copied from UnityEngine.UI.Selectable.
		/// </summary>
		protected override void OnCanvasGroupChanged()
		{
			// figure out if parent groups allow interaction
			bool doGroupsAllowInteraction = true;
			Transform t = this.transform;
			List<CanvasGroup> canvasGroups = ListPool<CanvasGroup>.Get();
			while (t != null)
			{
				t.GetComponents(canvasGroups);
				for (var i = 0; i < canvasGroups.Count; ++i)
				{
					if (!canvasGroups[i].interactable)
					{
						doGroupsAllowInteraction = false;
						break;
					}
					if (canvasGroups[i].ignoreParentGroups)
					{
						break;
					}
				}
				t = t.parent;
			}
			ListPool<CanvasGroup>.Release(canvasGroups);
			// trigger a state change if needed
			if (doGroupsAllowInteraction != m_DoGroupsAllowInteraction)
			{
				m_DoGroupsAllowInteraction = doGroupsAllowInteraction;
				OnInteractableChanged();
			}
		}

		/// <summary>
		/// Raises the click link event.
		/// </summary>
		/// <param name="link">Link.</param>
		private void OnClickLink(Link link)
		{
			if (link == m_HitboxOnPointerDown && link != null)
			{
				m_OnClick.Invoke(this, link.Info);
			}
			m_HitboxOnPointerDown = null; // NOTE: done here because this event is raised after OnPointerUp()
		}
		
		/// <summary>
		/// Raises the destroy event.
		/// </summary>
		protected override void OnDestroy()
		{
			base.OnDestroy();
			this.TextProcessor.Dispose();
		}
		
		/// <summary>
		/// Raises the disable event.
		/// </summary>
		protected override void OnDisable()
		{
			base.OnDisable();
			FontUpdateTracker.UntrackHyperText(this);
			ClearQuadMaskMaterial();
			for (int i = 0; i < m_QuadRenderersPool.Count; ++i)
			{
				if (m_QuadRenderersPool[i] != null)
				{
					m_QuadRenderersPool[i].Clear();
				}
			}
		}

		/// <summary>
		/// Raises the enable event.
		/// </summary>
		protected override void OnEnable()
		{
			s_QuadTextureId = Shader.PropertyToID("_Quad");
			base.OnEnable();
			FontUpdateTracker.TrackHyperText(this);
			this.TextProcessor.OnEnable();
			this.TextProcessor.OnBecameDirty -= OnTextProcessorBecameDirty;
			this.TextProcessor.OnBecameDirty += OnTextProcessorBecameDirty;
			OnExternalDependencyChanged();
		}
		
		/// <summary>
		/// Raises the enter link event.
		/// </summary>
		/// <param name="link">Link.</param>
		private void OnEnterLink(Link link)
		{
			// do nothing if the link is already under the cursor
			if (m_HitboxUnderCursor == link)
			{
				return;
			}
			// process as a press if e.g., click started, moved off, and moved back on
			if (m_HitboxUnderCursor == null && m_HitboxOnPointerDown != null && link == m_HitboxOnPointerDown)
			{
				m_HitboxUnderCursor = link;
				m_HitboxCandidate = m_HitboxOnPointerDown;
				m_HitboxOnPointerDown = null;
				OnPressLink(m_HitboxCandidate);
			}
			else
			{
				// otherwise exit the link previously under the cursor
				OnExitLink(m_HitboxUnderCursor);
				// store the link under the cursor and highlight it
				m_HitboxUnderCursor = link;
				if (m_HitboxUnderCursor != null)
				{
					DoLinkStateTransition(m_HitboxUnderCursor, LinkSelectionState.Highlighted);
					m_OnEnter.Invoke(this, m_HitboxUnderCursor.Info);
				}
			}
		}
		
		/// <summary>
		/// Raises the exit link event.
		/// </summary>
		/// <param name="link">Link.</param>
		private void OnExitLink(Link link)
		{
			// do nothing if null
			if (link == null)
			{
				return;
			}
			// clear link under cursor if it was exited
			if (link == m_HitboxUnderCursor)
			{
				m_HitboxUnderCursor = null;
			}
			// transition supplied link back to normal
			DoLinkStateTransition(link, LinkSelectionState.Normal);
			// fire off events
			m_OnExit.Invoke(this, link.Info);
		}

		/// <summary>
		/// Raises the external dependency changed event.
		/// </summary>
		private void OnExternalDependencyChanged()
		{
			if (m_ShouldInvokeExternalDependencyCallback)
			{
				FontUpdateTracker.UntrackHyperText(this);
				FontUpdateTracker.TrackHyperText(this);
				this.cachedTextGenerator.Invalidate();
				SetAllDirty();
			}
		}

#if IS_VBO_UI_VERTEX
		/// <summary>
		/// Raises the fill VBO event.
		/// </summary>
		/// <param name="vertexBufferObject">Vertex buffer object.</param>
		protected override void OnFillVBO(List<UIVertex> vertexBufferObject)
#elif UNITY_5_2_0 || UNITY_5_2_1
		/// <summary>
		/// Raises the populate mesh event.
		/// </summary>
		/// <param name="glyphMesh">Mesh to fill.</param>
		protected override void OnPopulateMesh(Mesh glyphMesh)
#else
		/// <summary>
		/// Raises the populate mesh event.
		/// </summary>
		/// <param name="vertexHelper">Vertex buffer object.</param>
		protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vertexHelper)
#endif
		{
			// NOTE: Early out if already inside this method (i.e. font texture changed callback is disabled).
			// For some reason, the first call to cachedTextGenerator.Populate() triggers an immediate call to
			// UpdateGeometry() on Snapdragon 805/Adreno 420 devices.
			if (this.FontToUse == null || m_DisableFontTextureChangedCallback)
			{
				return;
			}
			// disable font texture changed callback
			m_DisableFontTextureChangedCallback = true;
			// get UI vertices from text generator
			Rect inputRect = this.rectTransform.rect;
			this.cachedTextGenerator.Populate(PostprocessText(), GetGenerationSettings(inputRect.size));
			Vector2 textAnchorPivot = GetTextAnchorPivot(this.alignment);
			Vector2 refPoint = Vector2.zero;
			refPoint.x = (textAnchorPivot.x == 1f) ? inputRect.xMax : inputRect.xMin;
			refPoint.y = (textAnchorPivot.y == 0f) ? inputRect.yMin : inputRect.yMax;
			Vector2 roundingOffset = PixelAdjustPoint(refPoint) - refPoint;
			this.cachedTextGenerator.GetVertices(m_UIVertices);
			UIVertex vertex;
			float unitsPerPixel = 1f / this.pixelsPerUnit;
			if (roundingOffset != Vector2.zero)
			{
				for (int i = 0; i < m_UIVertices.Count; ++i)
				{
					vertex = m_UIVertices[i];
					vertex.position *= unitsPerPixel;
					vertex.position.x = vertex.position.x + roundingOffset.x;
					vertex.position.y = vertex.position.y + roundingOffset.y;
					m_UIVertices[i] = vertex;
				}
			}
			else
			{
				for (int i = 0; i < m_UIVertices.Count; ++i)
				{
					vertex = m_UIVertices[i];
					vertex.position *= unitsPerPixel;
					m_UIVertices[i] = vertex;
				}
			}
			// set final position, color, and UV for quads
			for (int quadIndex = 0; quadIndex < m_QuadGeometryData.Count; ++quadIndex)
			{
				if (m_QuadGeometryData[quadIndex].VertexIndices.EndIndex >= m_UIVertices.Count)
				{
					continue;
				}
				Rect uv = m_QuadGeometryData[quadIndex].UVRect;
				s_UVTransform[0] = new Vector2(uv.min.x, uv.max.y); // (0, 1)
				s_UVTransform[1] = new Vector2(uv.max.x, uv.max.y); // (1, 1)
				s_UVTransform[2] = new Vector2(uv.max.x, uv.min.y); // (1, 0)
				s_UVTransform[3] = new Vector2(uv.min.x, uv.min.y); // (0, 0)
				int scrollIndex = 0;
				bool clearColor = !m_QuadGeometryData[quadIndex].Style.ShouldRespectColorization;
				for (int i = 0; i < m_QuadGeometryData[quadIndex].VertexIndices.Count; ++i)
				{
					int vertexIndex = m_QuadGeometryData[quadIndex].VertexIndices[i];
					vertex = m_UIVertices[vertexIndex];
					vertex.position += Vector3.up * m_QuadGeometryData[quadIndex].GetVerticalOffset(this.FontSizeToUse);
					if (clearColor)
					{
						vertex.color = s_UntintedVertexColor;
					}
					vertex.uv0 = s_UVTransform[scrollIndex];
					m_UIVertices[vertexIndex] = vertex;
					++scrollIndex;
				}
			}
			// apply vertical offsets to all link and custom text styles
			Vector3 offset;
			foreach (
				TagGeometryData tagData in Enumerable.Concat(
					m_LinkGeometryData.Cast<TagGeometryData>(), m_CustomTagGeometryData.Cast<TagGeometryData>()
				)
			)
			{
				offset = tagData.GetVerticalOffset(this.FontSizeToUse) * Vector3.up;
				for (int i = 0; i < tagData.VertexIndices.Count; ++i)
				{
					{
						int vertexIndex = tagData.VertexIndices[i];
						if (vertexIndex >= m_UIVertices.Count)
						{
							continue;
						}
						vertex = m_UIVertices[vertexIndex];
						vertex.position += offset;
						m_UIVertices[vertexIndex] = vertex;
					}
				}
			}
			// get all the effects on this object
			List<Component> effects = ListPool<Component>.Get();
#if IS_VBO_UI_VERTEX
			GetComponents(typeof(UnityEngine.UI.IVertexModifier), effects);
#else
			GetComponents(typeof(UnityEngine.UI.IMeshModifier), effects);
#endif
			// offset values in character index tables to account for vertex modifier effects
			List<IndexRange> customTagIndexRanges = ListPool<IndexRange>.Get();
			customTagIndexRanges.AddRange(from l in m_LinkGeometryData select l.VertexIndices);
			customTagIndexRanges.AddRange(from q in m_QuadGeometryData select q.VertexIndices);
			customTagIndexRanges.AddRange(from t in m_CustomTagGeometryData select t.VertexIndices);
			List<List<IndexRange>> customTagRedrawIndexRanges = ListPool<List<IndexRange>>.Get();
			customTagRedrawIndexRanges.AddRange(from l in m_LinkGeometryData select l.RedrawVertexIndices);
			customTagRedrawIndexRanges.AddRange(from q in m_QuadGeometryData select q.RedrawVertexIndices);
			customTagRedrawIndexRanges.AddRange(from t in m_CustomTagGeometryData select t.RedrawVertexIndices);
#pragma warning disable 219
			MeshTopology meshLayout = PostprocessVertexIndexRanges(
				effects, m_TextGeneratorInput, customTagIndexRanges, customTagRedrawIndexRanges
			);
			ListPool<List<IndexRange>>.Release(customTagRedrawIndexRanges);
			ListPool<IndexRange>.Release(customTagIndexRanges);
#pragma warning restore 219
#if IS_VBO_UI_VERTEX
			// apply any vertex modification effects to cached vertex buffer
			for (int i = 0; i < effects.Count; ++i)
			{
				if (
					(effects[i] is Behaviour && !((Behaviour)effects[i]).enabled) ||
					!(effects[i] is UnityEngine.UI.IVertexModifier)
				)
				{
					continue;
				}
				((UnityEngine.UI.IVertexModifier)effects[i]).ModifyVertices(m_UIVertices);
			}
			// cache pre-degenerated vertex positions and base vertex colors
			m_BaseVertexColors.Clear();
			m_VertexPositions.Clear();
			for (int i = 0; i < m_UIVertices.Count; ++i)
			{
				m_BaseVertexColors.Add(m_UIVertices[i].color);
				m_VertexPositions.Add(m_UIVertices[i].position);
			}
#endif
			// fill vertex buffer
			List<IndexRange> indexRanges = ListPool<IndexRange>.Get();
#if IS_VBO_MESH
	#if UNITY_5_2_0 || UNITY_5_2_1
			using (UnityEngine.UI.VertexHelper vertexHelper = new UnityEngine.UI.VertexHelper())
	#else
			vertexHelper.Clear();
	#endif
			{
				for (int i = 0; i < m_UIVertices.Count; ++i)
				{
					int quadIdx = i & 3;
					s_GlyphVertices[quadIdx] = m_UIVertices[i];
					if (quadIdx == 3)
					{
						vertexHelper.AddUIVertexQuad(s_GlyphVertices);
					}
				}
	#if UNITY_5_2_0 || UNITY_5_2_1
				vertexHelper.FillMesh(glyphMesh);
	#endif
			}
			// apply any vertex modification effects to cached vertex buffer
			for (int i = 0; i < effects.Count; ++i)
			{
				if (!(effects[i] is Behaviour) || !((Behaviour)effects[i]).enabled)
				{
					continue;
				}
	#if UNITY_5_2_0 || UNITY_5_2_1
				((UnityEngine.UI.IMeshModifier)effects[i]).ModifyMesh(glyphMesh);
	#else
				((UnityEngine.UI.IMeshModifier)effects[i]).ModifyMesh(vertexHelper);
	#endif
			}
			ListPool<Component>.Release(effects);
	#if !(UNITY_5_2_0 || UNITY_5_2_1)
			Mesh glyphMesh = this.GlyphMesh;
			vertexHelper.FillMesh(glyphMesh);
	#endif
			// store colors and vertex positions
			m_VertexColors.Clear();
			m_VertexColors.AddRange(this.GlyphMesh.colors32);
			m_BaseVertexColors.Clear();
			m_BaseVertexColors.AddRange(this.GlyphMesh.colors32);
			m_VertexPositions.Clear();
			m_VertexPositions.AddRange(this.GlyphMesh.vertices);
			// set up quad materials
			m_QuadMaterials.Clear();
			m_QuadTextures.Clear();
			Texture2D quadTx;
			for (int quadIdx = 0; quadIdx < m_QuadGeometryData.Count; ++quadIdx)
			{
				quadTx = m_QuadGeometryData[quadIdx].Style.Sprite == null ?
					Texture2D.whiteTexture : m_QuadGeometryData[quadIdx].Style.Sprite.texture;
				if (!m_QuadMaterials.ContainsKey(quadTx))
				{
					m_QuadMaterials[quadTx] = null;
					m_QuadTextures.Add(quadTx);
				}
			}
			// copy mesh data to quad canvas renderers and degenerate quad vertices on text VBO
			List<Vector3> textNormals = ListPool<Vector3>.Get();
			List<Vector4> textTangents = ListPool<Vector4>.Get();
			List<Vector2> textUV1 = ListPool<Vector2>.Get();
			List<Vector2> textUV2 = ListPool<Vector2>.Get();
			List<Vector3> textVertices = ListPool<Vector3>.Get();
			textNormals.AddRange(glyphMesh.normals);
			textTangents.AddRange(glyphMesh.tangents);
			textUV1.AddRange(glyphMesh.uv);
			textUV2.AddRange(glyphMesh.uv2);
			textVertices.AddRange(glyphMesh.vertices);
			List<Vector3> quadNormals = ListPool<Vector3>.Get();
			List<Vector4> quadTangents = ListPool<Vector4>.Get();
			List<int> quadTriangles = ListPool<int>.Get();
			List<Vector2> quadUV1 = ListPool<Vector2>.Get();
			List<Vector2> quadUV2 = ListPool<Vector2>.Get();
			List<Color32> quadVertexColors = ListPool<Color32>.Get();
			List<Vector3> quadVertices = ListPool<Vector3>.Get();
			for (int quadIndex = 0; quadIndex < m_QuadGeometryData.Count; ++quadIndex)
			{
				indexRanges.Clear();
				indexRanges.AddRange(m_QuadGeometryData[quadIndex].RedrawVertexIndices);
				indexRanges.Add(m_QuadGeometryData[quadIndex].VertexIndices);
				quadVertices.Clear();
				quadNormals.Clear();
				quadTangents.Clear();
				quadTriangles.Clear();
				quadUV1.Clear();
				quadUV2.Clear();
				quadVertexColors.Clear();
				for (int i = 0; i < indexRanges.Count; ++i)
				{
					if (indexRanges[i].StartIndex >= textVertices.Count)
					{
						continue;
					}
					for (int j = 0; j < indexRanges[i].Count; ++j)
					{
						int vertexIndex = indexRanges[i][j];
						if (vertexIndex >= textVertices.Count)
						{
							continue;
						}
						quadNormals.Add(textNormals[vertexIndex]);
						quadTangents.Add(textTangents[vertexIndex]);
						quadUV1.Add(textUV1[vertexIndex]);
						quadUV2.Add(textUV2[vertexIndex]);
						quadVertexColors.Add(m_VertexColors[vertexIndex]);
						quadVertices.Add(textVertices[vertexIndex]);
						textVertices[vertexIndex] = textVertices[indexRanges[i].StartIndex];
					}
					int baseIdx;
					switch (meshLayout)
					{
					case MeshTopology.Quads:
						baseIdx = i * 4;
						quadTriangles.Add(baseIdx);
						quadTriangles.Add(baseIdx + 1);
						quadTriangles.Add(baseIdx + 2);
						quadTriangles.Add(baseIdx + 2);
						quadTriangles.Add(baseIdx + 3);
						quadTriangles.Add(baseIdx);
						break;
					case MeshTopology.Triangles:
						baseIdx = i * 6;
						for (int j = 0; j < indexRanges[i].Count; ++j)
						{
							quadTriangles.Add(baseIdx + j);
						}
						break;
					}
				}
				m_QuadMeshes[quadIndex].Clear();
				m_QuadMeshes[quadIndex].SetVertices(quadVertices);
				m_QuadMeshes[quadIndex].SetNormals(quadNormals);
				m_QuadMeshes[quadIndex].SetTangents(quadTangents);
				m_QuadMeshes[quadIndex].SetTriangles(quadTriangles, 0);
				m_QuadMeshes[quadIndex].SetUVs(0, quadUV1);
				m_QuadMeshes[quadIndex].SetUVs(1, quadUV2);
			}
			glyphMesh.SetVertices(textVertices);
			ListPool<Vector3>.Release(quadNormals);
			ListPool<Vector4>.Release(quadTangents);
			ListPool<int>.Release(quadTriangles);
			ListPool<Vector2>.Release(quadUV1);
			ListPool<Vector2>.Release(quadUV2);
			ListPool<Color32>.Release(quadVertexColors);
			ListPool<Vector3>.Release(quadVertices);
			ListPool<Vector3>.Release(textNormals);
			ListPool<Vector4>.Release(textTangents);
			ListPool<Vector2>.Release(textUV1);
			ListPool<Vector2>.Release(textUV2);
			ListPool<Vector3>.Release(textVertices);
#else
			// degenerate quad vertices on text VBO
			for (int quadIndex = 0; quadIndex < m_QuadGeometryData.Count; ++quadIndex)
			{
				indexRanges.Clear();
				indexRanges.AddRange(m_QuadGeometryData[quadIndex].RedrawVertexIndices);
				indexRanges.Add(m_QuadGeometryData[quadIndex].VertexIndices);
				for (int i = 0; i < indexRanges.Count; ++i)
				{
					for (int j = 0; j < indexRanges[i].Count; ++j)
					{
						int vertexIndex = indexRanges[i][j];
						if (vertexIndex >= m_UIVertices.Count)
						{
							continue;
						}
						vertex = m_UIVertices[vertexIndex];
						vertex.position = m_UIVertices[indexRanges[i].StartIndex].position;
						m_UIVertices[vertexIndex] = vertex;
					}
				}
			}
			vertexBufferObject.AddRange(m_UIVertices);
#endif
			ListPool<IndexRange>.Release(indexRanges);
			// populate hitboxes of links
			UpdateLinkHitboxRects();
			// re-enable font texture changed callback
			m_DisableFontTextureChangedCallback = false;
		}

		/// <summary>
		/// Raises the interactable changed event, which initiates link state transitions.
		/// </summary>
		private void OnInteractableChanged()
		{
			// if application is not playing, do immediate color change
			if (!Application.isPlaying)
			{
				UpdateGeometry();
			}
			// otherwise initiate state transition
			else
			{
				// NOTE: Unity always triggers UpdateGeometry() from inspector, so manual transition is immediate
				LinkSelectionState newState =
					IsInteractable() ? LinkSelectionState.Normal : LinkSelectionState.Disabled;
				for (int i = 0; i < m_LinkGeometryData.Count; ++i)
				{
					DoLinkStateTransition(m_LinkGeometryData[i], newState);
				}
			}
		}
		
		/// <summary>
		/// Raises the pointer click event.
		/// </summary>
		/// <param name="eventData">Event data.</param>
		public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
		{
			if (!IsActive())
			{
				return;
			}
			OnClickLink(GetLinkAtPointerPosition(eventData.position, eventData.pressEventCamera));
		}

		/// <summary>
		/// Raises the pointer down event.
		/// </summary>
		/// <param name="eventData">Event data.</param>
		public void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData)
		{
			if (!IsActive())
			{
				return;
			}
			OnPressLink(GetLinkAtPointerPosition(eventData.pressPosition, eventData.pressEventCamera));
		}
		
		/// <summary>
		/// Updates the mouseover state.
		/// </summary>
		/// <param name="eventData">Event data.</param>
		public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
		{
			if (!IsActive())
			{
				return;
			}
			m_MostRecentEnterEventCamera = eventData.enterEventCamera;
			OnEnterLink(GetLinkAtPointerPosition(eventData.position, eventData.enterEventCamera));
		}

		
		/// <summary>
		/// Updates the mouseover state.
		/// </summary>
		/// <param name="eventData">Event data.</param>
		public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
		{
			if (!IsActive() || m_HitboxUnderCursor == null)
			{
				return;
			}
			OnExitLink(m_HitboxUnderCursor);
		}
		
		/// <summary>
		/// Raises the pointer up event.
		/// </summary>
		/// <param name="eventData">Event data.</param>
		public void OnPointerUp(UnityEngine.EventSystems.PointerEventData eventData)
		{
			if (!IsActive())
			{
				return;
			}
			OnReleaseLink(GetLinkAtPointerPosition(eventData.position, eventData.pressEventCamera));
		}
		
		/// <summary>
		/// Raises the press link event.
		/// </summary>
		/// <param name="link">Link.</param>
		private void OnPressLink(Link link)
		{
			if (m_HitboxOnPointerDown == link || link == null)
			{
				return;
			}
			m_HitboxOnPointerDown = link;
			DoLinkStateTransition(m_HitboxOnPointerDown, LinkSelectionState.Pressed);
			m_OnPress.Invoke(this, m_HitboxOnPointerDown.Info);
		}

#if UNITY_EDITOR
		/// <summary>
		/// Raises the rebuild requested event.
		/// </summary>
		public override void OnRebuildRequested()
		{
			FontUpdateTracker.UntrackHyperText(this);
			FontUpdateTracker.TrackHyperText(this);
			base.OnRebuildRequested();
		}
#endif

		/// <summary>
		/// Raises the release link event.
		/// </summary>
		/// <param name="link">Link.</param>
		private void OnReleaseLink(Link link)
		{
			if (link != m_HitboxOnPointerDown || m_HitboxOnPointerDown == null)
			{
				return;
			}
			DoLinkStateTransition(m_HitboxOnPointerDown, LinkSelectionState.Highlighted);
			m_OnRelease.Invoke(this, m_HitboxOnPointerDown.Info);
		}

		/// <summary>
		/// Raises the text processor became dirty event.
		/// </summary>
		/// <param name="textSource">The <see cref="ITextSource"/> that triggered the event.</param>
		private void OnTextProcessorBecameDirty(ITextSource textSource)
		{
			OnExternalDependencyChanged();
		}

#if UNITY_EDITOR
		/// <summary>
		/// Raises the validate event.
		/// </summary>
		protected override void OnValidate()
		{
			base.OnValidate();
			ClearQuadMaskMaterial();
		}
#endif

		/// <summary>
		/// Opens the API reference page.
		/// </summary>
		[ContextMenu("API Reference")]
		private void OpenAPIReferencePage()
		{
			this.OpenReferencePage("uas-hypertext");
		}

		/// <summary>
		/// Using the supplied list of vertex/mesh modifiers and the text generator input string, apply offsets to
		/// vertex index ranges to reflect how the vertex modifiers shift indices of UI vertices. Override this method
		/// if you need to account for custom UI.IVertexModifier / UI.IMeshModifier effects that use more discriminating
		/// methods.
		/// </summary>
		/// <returns>
		/// <see cref="UnityEngine.MeshTopology.Quads"/> if the modified vertices will have quad layout; otherwise,
		/// <see cref="UnityEngine.MeshTopology.Triangles"/>.
		/// </returns>
		/// <param name="modifiers">All vertex or mesh modifiers on the object.</param>
		/// <param name="textGeneratorInputValue">The string submitted to cachedTextGenerator.</param>
		/// <param name="customTagVertexIndices">
		/// Range of vertex indices for all link, custom text style, and quad geometry.
		/// </param>
		/// <param name="customTagRedrawVertexIndices">
		/// Ranges of vertex indices for any redrawn links, custom text, and quad geometry in TextGenerator output.
		/// </param>
		protected virtual MeshTopology PostprocessVertexIndexRanges(
			List<Component> modifiers,
			string textGeneratorInputValue,
			List<IndexRange> customTagVertexIndices,
			List<List<IndexRange>> customTagRedrawVertexIndices
		)
		{
			// determine number of draws
			int numDraws = 1;
#pragma warning disable 219
			bool doesChangeTopology = false;
#pragma warning restore 219
			for (int i = 0; i < modifiers.Count; ++i)
			{
				if (!(modifiers[i] is Behaviour) || !((Behaviour)modifiers[i]).enabled)
				{
					continue;
				}
				doesChangeTopology = true;
				if (modifiers[i] is UnityEngine.UI.Outline) // inherits from Shadow, so test first
				{
					numDraws *= 5;
				}
				else if (modifiers[i] is UnityEngine.UI.Shadow)
				{
					numDraws *= 2;
				}
			}
			// determine offset amount (NOTE: use actual vertices generated to account for clipping)
			this.cachedTextGenerator.Populate(
				textGeneratorInputValue, GetGenerationSettings(this.rectTransform.rect.size)
			);
			int vertexScrollPerDraw = this.cachedTextGenerator.vertexCount;
			MeshTopology result = MeshTopology.Quads;
#if IS_VBO_MESH
			if (doesChangeTopology)
			{
				result = MeshTopology.Triangles;
			}
			// NOTE: Shadow and Outline generate UIVertex stream from mesh triangle list; every quad becomes 6 vertices
			vertexScrollPerDraw += vertexScrollPerDraw / 2;
#endif
			int totalScroll = 0;
			if (numDraws > 1)
			{
				totalScroll = vertexScrollPerDraw * (numDraws - 1);
			}
			// offset index ranges for custom tags and populate lists of index ranges for redrawn characters
			for (int tagIndex = 0; tagIndex < customTagRedrawVertexIndices.Count; ++tagIndex)
			{
				IndexRange vertexIndexRange = customTagVertexIndices[tagIndex];
				int baseStart = vertexIndexRange.StartIndex;
				int count = vertexIndexRange.Count;
#if IS_VBO_MESH
				if (result == MeshTopology.Triangles)
				{
					baseStart = (int)(vertexIndexRange.StartIndex * 1.5f);
					count += count / 2;
				}
#endif
				int baseEnd = baseStart + count - 1;
				for (int drawPass = 0; drawPass < numDraws - 1; ++drawPass)
				{
					int scroll = drawPass * vertexScrollPerDraw;
					customTagRedrawVertexIndices[tagIndex].Add(new IndexRange(baseStart + scroll, baseEnd + scroll));
				}
				vertexIndexRange.StartIndex = baseStart + totalScroll;
				vertexIndexRange.EndIndex = vertexIndexRange.StartIndex + count - 1;
			}
			return result;
		}

		/// <summary>
		/// Postprocess the text data before submitting it to cachedTextGenerator.
		/// </summary>
		private string PostprocessText()
		{
			UpdateTextProcessor();
			// clear existing data
			m_LinkGeometryData.Clear();
			m_CustomTagGeometryData.Clear();
			m_QuadGeometryData.Clear();
			m_QuadRenderersPool.RemoveAll(quadRenderer => quadRenderer == null);
			for (int i = 0; i < m_QuadRenderersPool.Count; ++i)
			{
				m_QuadRenderersPool[i].Clear();
			}
			// copy link data
			List<HyperTextProcessor.Link> linkCharacterData = ListPool<HyperTextProcessor.Link>.Get();
			this.TextProcessor.GetLinks(ref linkCharacterData);
			for (int i = 0; i < linkCharacterData.Count; ++i)
			{
				m_LinkGeometryData.Add(new Link(i, linkCharacterData[i], this));
			}
			ListPool<HyperTextProcessor.Link>.Release(linkCharacterData);
			// set up other rich tags if enabled
			if (this.TextProcessor.IsRichTextEnabled)
			{
				// add custom text style tag geometry data
				List<HyperTextProcessor.CustomTag> tagCharacterData = ListPool<HyperTextProcessor.CustomTag>.Get();
				this.TextProcessor.GetCustomTags(ref tagCharacterData);
				for (int i = 0; i < tagCharacterData.Count; ++i)
				{
					m_CustomTagGeometryData.Add(new CustomTag(tagCharacterData[i]));
				}
				ListPool<HyperTextProcessor.CustomTag>.Release(tagCharacterData);
				// set up quads if the current object is not a prefab and does not use sub-meshes on the main canvas
				if (!this.IsPrefab)
				{
					m_QuadTracker.Clear();
					RectTransform quadTransform = null;
					List<HyperTextProcessor.Quad> quadCharacterData = ListPool<HyperTextProcessor.Quad>.Get();
					this.TextProcessor.GetQuads(ref quadCharacterData);
					for (int matchIndex = 0; matchIndex < quadCharacterData.Count; ++matchIndex)
					{
						// TODO: switch over to ObjectX.GetFromPool()
						// add new quad data to list
						m_QuadGeometryData.Add(new Quad(quadCharacterData[matchIndex]));
						// grow pool if needed
						if (matchIndex >= m_QuadRenderersPool.Count)
						{
							GameObject newQuadObject =
							new GameObject("<quad>", typeof(RectTransform), typeof(CanvasRenderer));
							m_QuadRenderersPool.Add(newQuadObject.GetComponent<CanvasRenderer>());
#if UNITY_EDITOR
							// ensure changes to prefab instances' pools get serialized when not selected
							if (!Application.isPlaying)
							{
								UnityEditor.EditorUtility.SetDirty(this);
							}
#endif
						}
#if IS_VBO_MESH
						if (matchIndex >= m_QuadMeshes.Count)
						{
							Mesh mesh = new Mesh();
							mesh.hideFlags = HideFlags.HideAndDontSave;
							m_QuadMeshes.Add(mesh);
						}
#endif
						// make sure layer is the same
						m_QuadRenderersPool[matchIndex].gameObject.layer = this.gameObject.layer;
						// lock transform
						quadTransform = m_QuadRenderersPool[matchIndex].transform as RectTransform;
						if (quadTransform != null)
						{
							quadTransform.SetParent(this.rectTransform);
							m_QuadTracker.Add(this, quadTransform, DrivenTransformProperties.All);
							quadTransform.anchorMax = Vector2.one;
							quadTransform.anchorMin = Vector2.zero;
							quadTransform.sizeDelta = Vector2.zero;
							quadTransform.pivot = this.rectTransform.pivot;
							quadTransform.localPosition = Vector3.zero;
							quadTransform.localRotation = Quaternion.identity;
							quadTransform.localScale = Vector3.one;
						}
						// configure quad
						m_QuadGeometryData[matchIndex].Renderer = m_QuadRenderersPool[matchIndex];
						m_QuadGeometryData[matchIndex].Renderer.Clear();
					}
					ListPool<HyperTextProcessor.Quad>.Release(quadCharacterData);
				}
			}
			m_TextGeneratorInput = this.TextProcessor.OutputText;
			return m_TextGeneratorInput;
		}

		/// <summary>
		/// A custom raycast callback to determine if there is a link hit box under the pointer position.
		/// </summary>
		/// <returns>
		/// <see langword="true"/>, if pointer position is over a link hit box; otherwise, <see langword="false"/>.
		/// </returns>
		/// <param name="pointerPosition">Pointer position.</param>
		/// <param name="eventCamera">Event camera.</param>
		public override bool Raycast(Vector2 pointerPosition, Camera eventCamera)
		{
			// early out if links are disabled or base raycast fails
			if (!IsInteractable() || !base.Raycast(pointerPosition, eventCamera))
			{
				return false;
			}
			m_HitboxCandidate = GetLinkAtPointerPosition(pointerPosition, eventCamera);
			if (m_HitboxCandidate == null)
			{
				return false;
			}
			return true;
		}

#if IS_VBO_MESH
		/// <summary>
		/// Sets the clipping rectangle on this object's <see cref="UnityEngine.CanvasRenderer"/>.
		/// </summary>
		/// <param name="clipRect">Clipping rectangle.</param>
		/// <param name="validRect">
		/// If set to <see langword="true"/> then <paramref name="clipRect"/> is a valid rectangle.
		/// </param>
		public override void SetClipRect(Rect clipRect, bool validRect)
		{
			base.SetClipRect(clipRect, validRect);
			for (int i = 0; i < m_QuadRenderersPool.Count; ++i)
			{
				if (m_QuadRenderersPool[i] != null)
				{
					if (validRect)
					{
						m_QuadRenderersPool[i].EnableRectClipping(clipRect);
					}
					else
					{
						m_QuadRenderersPool[i].DisableRectClipping();
					}
				}
			}
		}
#endif

		/// <summary>
		/// Sets the link keyword collections.
		/// </summary>
		/// <remark>Included for inspector.</remark>
		/// <param name="value">Value.</param>
		private void SetLinkKeywordCollections(HyperTextProcessor.KeywordCollectionClass[] value)
		{
			SetLinkKeywordCollections(value as IEnumerable<HyperTextProcessor.KeywordCollectionClass>);
		}

		/// <summary>
		/// Sets the link keyword collections.
		/// </summary>
		/// <param name="value">Value.</param>
		public void SetLinkKeywordCollections(IEnumerable<HyperTextProcessor.KeywordCollectionClass> value)
		{
			this.TextProcessor.SetLinkKeywordCollections(value);
		}

		/// <summary>
		/// Sets the material dirty.
		/// </summary>
		public override void SetMaterialDirty()
		{
			base.SetMaterialDirty();
			ClearQuadMaskMaterial();
		}

		/// <summary>
		/// Sets the quad keyword collections.
		/// </summary>
		/// <remark>Included for inspector.</remark>
		/// <param name="value">Value.</param>
		private void SetQuadKeywordCollections(HyperTextProcessor.KeywordCollectionClass[] value)
		{
			SetQuadKeywordCollections(value as IEnumerable<HyperTextProcessor.KeywordCollectionClass>);
		}

		/// <summary>
		/// Sets the quad keyword collections.
		/// </summary>
		/// <param name="value">Value.</param>
		public void SetQuadKeywordCollections(IEnumerable<HyperTextProcessor.KeywordCollectionClass> value)
		{
			this.TextProcessor.SetQuadKeywordCollections(value);
		}

		/// <summary>
		/// Sets the tag keyword collections.
		/// </summary>
		/// <remark>Included for inspector.</remark>
		/// <param name="value">Value.</param>
		private void SetTagKeywordCollections(HyperTextProcessor.KeywordCollectionClass[] value)
		{
			SetTagKeywordCollections(value as IEnumerable<HyperTextProcessor.KeywordCollectionClass>);
		}

		/// <summary>
		/// Sets the tag keyword collections.
		/// </summary>
		/// <param name="value">Value.</param>
		public void SetTagKeywordCollections(IEnumerable<HyperTextProcessor.KeywordCollectionClass> value)
		{
			this.TextProcessor.SetTagKeywordCollections(value);
		}

		/// <summary>
		/// Sets the vertices dirty.
		/// </summary>
		public override void SetVerticesDirty()
		{
			base.SetVerticesDirty();
			m_AreVerticesDirty = true;
		}
		
		/// <summary>
		/// Initialize this <see cref="HyperText"/>.
		/// </summary>
		protected override void Start()
		{
			base.Start();
			if (this.Styles == null && m_DefaultStyles != null)
			{
				this.Styles = m_DefaultStyles;
				if (
					m_DefaultStyles.CascadedFont != null && this.font == Resources.GetBuiltinResource<Font>("Arial.ttf")
				)
				{
					this.font = null;
				}
			}
		}

		/// <summary>
		/// Update vertex colors on this instance.
		/// </summary>
		protected virtual void Update()
		{
			if (Application.isPlaying)
			{
				// force state transitions if link under cursor changed; prevents false misses from fast mouse movements
				if (IsInteractable() && Input.mousePresent && m_HitboxUnderCursor != null)
				{
					m_HitboxCandidate = GetLinkAtPointerPosition(Input.mousePosition, m_MostRecentEnterEventCamera);
					if (m_HitboxCandidate != m_HitboxUnderCursor)
					{
						OnEnterLink(m_HitboxCandidate);
					}
				}
				if (m_IsAnimatingLinkStateTransition)
				{
					UpdateVertexColors();
				}
			}
			m_IsAnimatingLinkStateTransition = false;
		}
		
		/// <summary>
		/// Updates the geometry.
		/// </summary>
		protected override void UpdateGeometry()
		{
			if (this.FontToUse == null)
			{
				m_AreVerticesDirty = false;
				return;
			}
			m_ShouldInvokeExternalDependencyCallback = false;
			// populate cachedTextGenerator, links, quads, and uiVertices
			// do not call base implementation of UpdateGeometry(), as it requires this.font to be set
			if (this.rectTransform.rect.width >= 0f && this.rectTransform.rect.height >= 0f)
			{
#if IS_VBO_UI_VERTEX
				List<UIVertex> vbo = ListPool<UIVertex>.Get();
				OnFillVBO(vbo);
				ListPool<UIVertex>.Release(vbo);
#elif UNITY_5_2_0 || UNITY_5_2_1
				OnPopulateMesh(this.GlyphMesh);
#else
				OnPopulateMesh(s_VertexHelper);
#endif
			}
			// update the renderer to set link colors
			UpdateVertexColors();
			m_ShouldInvokeExternalDependencyCallback = true;
			m_AreVerticesDirty = false;
			UpdateMaterial(); // TODO: why is this necessary when enabling/disabling modifiers with quads?
		}

		/// <summary>
		/// Updates the link hit box rectangles.
		/// </summary>
		private void UpdateLinkHitboxRects()
		{
			Bounds bounds;
			Vector3 position;
			ImmutableRectOffset padding = m_ShouldOverrideStylesLinkHitboxPadding || this.Styles == null ?
				m_LinkHitboxPadding : this.Styles.CascadedLinkHitboxPadding;
			List<Rect> hitboxes = ListPool<Rect>.Get();
			for (int linkIdx = 0; linkIdx < m_LinkGeometryData.Count; ++linkIdx)
			{
				hitboxes.Clear();
				if (m_LinkGeometryData[linkIdx].VertexIndices.StartIndex >= m_VertexPositions.Count)
				{
					continue;
				}
				position = m_VertexPositions[m_LinkGeometryData[linkIdx].VertexIndices.StartIndex];
				bounds = new Bounds(position, Vector3.zero);
				for (int i = 0; i < m_LinkGeometryData[linkIdx].VertexIndices.Count; ++i)
				{
					if (m_LinkGeometryData[linkIdx].VertexIndices[i] >= m_VertexPositions.Count)
					{
						continue;
					}
					position = m_VertexPositions[m_LinkGeometryData[linkIdx].VertexIndices[i]];
					if (position.x < bounds.min.x)
					{
						hitboxes.Add(
							new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y)
						);
						bounds = new Bounds(position, Vector3.zero);
					}
					else
					{
						bounds.Encapsulate(position);
					}
				}
				bounds.min -= new Vector3(padding.Left, padding.Bottom);
				bounds.max += new Vector3(padding.Right, padding.Top);
				hitboxes.Add(
					new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y)
				);
				m_LinkGeometryData[linkIdx].SetHitboxes(hitboxes);
			}
			ListPool<Rect>.Release(hitboxes);
		}

		/// <summary>
		/// Updates the material.
		/// </summary>
		protected override void UpdateMaterial()
		{
			base.UpdateMaterial();
			if (!IsActive())
			{
				return;
			}
			Material quadMaterialForRendering = this.QuadMaterialForRendering;
			for (int i = 0; i < m_QuadGeometryData.Count; ++i)
			{
				m_QuadGeometryData[i].Renderer.SetMaterial(quadMaterialForRendering, m_QuadGeometryData[i].Texture);
			}
		}

		/// <summary>
		/// Updates the text processor.
		/// </summary>
		private void UpdateTextProcessor()
		{
			this.TextProcessor.ReferenceFontSize = this.FontSizeToUse;
			this.TextProcessor.InputText = this.text;
			this.TextProcessor.IsDynamicFontDesired = this.FontToUse != null && this.FontToUse.dynamic;
			this.TextProcessor.IsRichTextDesired = this.supportRichText;
#if IS_TEXTGEN_SCALE_FACTOR_ABSENT
			this.TextProcessor.ScaleFactor = this.pixelsPerUnit;
#else
			this.TextProcessor.ScaleFactor = 1f;
#endif
		}

		/// <summary>
		/// Updates the vertex colors on all <see cref="UnityEngine.CanvasRenderer"/>s.
		/// </summary>
		private void UpdateVertexColors()
		{
#if IS_VBO_UI_VERTEX
			UIVertex vertex;
			int vertexCount = m_UIVertices.Count;
#else
			int vertexCount = m_VertexColors.Count;
#endif
			// colorize links
			for (int i = 0; i < m_LinkGeometryData.Count; ++i)
			{
				HyperTextStyles.Link style = m_LinkGeometryData[i].Style;
				Color stateColor = m_LinkGeometryData[i].Tint;
				for (
					int vertexIndex = m_LinkGeometryData[i].VertexIndices.StartIndex;
					vertexIndex < Mathf.Min(m_LinkGeometryData[i].VertexIndices.EndIndex + 1, vertexCount);
					++vertexIndex
				)
				{
					Color vertexColor = stateColor;
					Color baseColor = m_BaseVertexColors[vertexIndex];
					switch (style.ColorTintMode)
					{
					case ColorTintMode.Additive:
						vertexColor = stateColor + baseColor;
						break;
					case ColorTintMode.Constant:
						vertexColor = stateColor;
						break;
					case ColorTintMode.Multiplicative:
						vertexColor = stateColor * baseColor;
						break;
					}
					switch (style.ColorTweenMode)
					{
					case ColorTween.Mode.RGB:
						vertexColor.a = baseColor.a;
						break;
					case ColorTween.Mode.Alpha:
						vertexColor.r = baseColor.r;
						vertexColor.g = baseColor.g;
						vertexColor.b = baseColor.b;
						break;
					}
#if IS_VBO_UI_VERTEX
					vertex = m_UIVertices[vertexIndex];
					vertex.color = vertexColor;
					m_UIVertices[vertexIndex] = vertex;
#else
					m_VertexColors[vertexIndex] = vertexColor;
#endif
				}
			}
			// colorize quads and set the vertices on managed CanvasRenderers
			List<IndexRange> indexRanges = ListPool<IndexRange>.Get();
			List<UIVertex> quadUIVertices = ListPool<UIVertex>.Get();
			List<Color32> quadVertexColors = ListPool<Color32>.Get();
			for (int quadIndex = 0; quadIndex < m_QuadGeometryData.Count; ++quadIndex)
			{
				// empty out renderers for quads that are clipped
				if (m_QuadGeometryData[quadIndex].VertexIndices.EndIndex >= vertexCount)
				{
					m_QuadGeometryData[quadIndex].Renderer.Clear();
				}
				else
				{
					indexRanges.Clear();
					indexRanges.AddRange(m_QuadGeometryData[quadIndex].RedrawVertexIndices);
					indexRanges.Add(m_QuadGeometryData[quadIndex].VertexIndices);
					// copy colors from vertex list and apply to quad renderer
					quadUIVertices.Clear();
#if IS_VBO_MESH
					quadVertexColors.Clear();
#endif
					for (int i = 0; i < indexRanges.Count; ++i)
					{
						bool doSwizzle = i == indexRanges.Count - 1 && HyperText.ShouldSwizzleQuadRedBlue;
						bool clearColor = i == indexRanges.Count - 1 &&
							!m_QuadGeometryData[quadIndex].Style.ShouldRespectColorization;
						for (int j = 0; j < indexRanges[i].Count; ++j)
						{
							int vertexIndex = indexRanges[i][j];
							if (vertexIndex >= vertexCount)
							{
								continue;
							}
							Color32 vertexColor =
#if IS_VBO_UI_VERTEX
								m_UIVertices[vertexIndex].color;
#else
								m_VertexColors[vertexIndex];
#endif
							if (clearColor)
							{
								vertexColor = s_UntintedVertexColor;
							}
							else if (doSwizzle)
							{
								vertexColor = new Color32(vertexColor.b, vertexColor.g, vertexColor.r, vertexColor.a);
							}
#if IS_VBO_UI_VERTEX
							vertex = m_UIVertices[vertexIndex];
							vertex.color = vertexColor;
							vertex.position = m_VertexPositions[vertexIndex];
							quadUIVertices.Add(vertex);
#else
							quadVertexColors.Add(vertexColor);
#endif
						}
					}
#if IS_VBO_UI_VERTEX
					m_QuadGeometryData[quadIndex].Renderer.SetVertices(quadUIVertices);
#else
					m_QuadMeshes[quadIndex].SetColors(quadVertexColors);
					m_QuadGeometryData[quadIndex].Renderer.SetMesh(m_QuadMeshes[quadIndex]);
#endif
				}
			}
			ListPool<Color32>.Release(quadVertexColors);
			ListPool<UIVertex>.Release(quadUIVertices);
			ListPool<IndexRange>.Release(indexRanges);
#if IS_VBO_UI_VERTEX
			this.canvasRenderer.SetVertices(m_UIVertices);
#endif
#if IS_VBO_MESH
			this.GlyphMesh.SetColors(m_VertexColors);
			this.canvasRenderer.SetMesh(this.GlyphMesh);
#endif
		}

		#region Obsolete
		/// <summary>
		/// This method is obsolete. Use 
		/// <see cref="M:Candlelight.UI.HyperText.GetLinkHitboxes(System.Collections.Generic.Dictionary{Candlelight.UI.HyperText.LinkInfo,System.Collections.Generic.List{UnityEngine.Rect}}@)" /> 
		/// instead.
		/// </summary>
		/// <param name="hitboxes">A list of <see cref="LinkInfo"/> to populate.</param>
		[System.Obsolete("Use GetLinkHitboxes(ref Dictionary<LinkInfo, List<Rect>>)", true)]
		public void GetLinkHitboxes(ref List<LinkInfo> hitboxes)
		{
			Dictionary<LinkInfo, List<Rect>> linkHitboxes = new Dictionary<LinkInfo, List<Rect>>();
			GetLinkHitboxes(ref linkHitboxes);
			hitboxes = hitboxes ?? new List<LinkInfo>();
			hitboxes.Clear();
			hitboxes.AddRange(linkHitboxes.Keys);
		}
		#endregion
	}
}