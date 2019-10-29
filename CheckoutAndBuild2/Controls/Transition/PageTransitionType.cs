namespace FG.CheckoutAndBuild2.Controls.Transition
{
    /// <summary>
    /// Seitenübergänge
    /// </summary>
	public enum PageTransitionType
	{
        /// <summary>
        /// Keine Animation
        /// </summary>
        None,
        /// <summary>
        /// Fade
        /// </summary>
		Fade,
        /// <summary>
        /// Slide von rechts nach Links
        /// </summary>
		Slide,
        /// <summary>
        /// Slide + Fade
        /// </summary>
		SlideAndFade,
        /// <summary>
        /// Slide von Links nach Rechts
        /// </summary>
        ReverseSlide,
        /// <summary>
        /// Slide + Fade
        /// </summary>
        ReverseSlideAndFade,
        /// <summary>
        /// Grow
        /// </summary>
		Grow,
        /// <summary>
        /// GrowAndFade
        /// </summary>
		GrowAndFade,
        /// <summary>
        /// Flip
        /// </summary>
		Flip,
        /// <summary>
        /// Flip + Fade
        /// </summary>
		FlipAndFade,
        /// <summary>
        /// Spin
        /// </summary>
		Spin,
        /// <summary>
        /// SpinAndFade
        /// </summary>
		SpinAndFade
	}
}
