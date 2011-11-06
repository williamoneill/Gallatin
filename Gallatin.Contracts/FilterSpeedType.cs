namespace Gallatin.Contracts
{
    /// <summary>
    /// This enumeration is used by add-ins to determine their evaluation order. Duplicates filter speed will execute arbitrarily.
    /// </summary>
    /// <remarks>
    /// Developers, you can cheat and tag your plug-in as LocalAndFast when it is actually Remote. Do not do this.
    /// Just be honest so we fail fast and provide the best experience to the end user.
    /// </remarks>
    public enum FilterSpeedType
    {
        /// <summary>
        /// Decision can be made quickly and without remote resources
        /// </summary>
        LocalAndFast,

        /// <summary>
        /// Decision can be made locally but may be slow, for example using large regular expressions or heavy computation
        /// </summary>
        LocalAndSlow,

        /// <summary>
        /// Filter decision is made using a remote resource such as a web service
        /// </summary>
        Remote,
    }
}