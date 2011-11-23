namespace Gallatin.Filter.Util
{
    /// <summary>
    /// Setting file types
    /// </summary>
    public enum SettingsFileType
    {
        /// <summary>
        /// Whitelist XML
        /// </summary>
        Whitelist,

        /// <summary>
        /// Blacklist XML
        /// </summary>
        Blacklist,

        /// <summary>
        /// Mime type filter XML
        /// </summary>
        MimeTypeFilter,

        /// <summary>
        /// File extenstion filter XML
        /// </summary>
        ExtensionFilter,

        /// <summary>
        /// HTML body filter XML
        /// </summary>
        HtmlBodyFilter
    }
}