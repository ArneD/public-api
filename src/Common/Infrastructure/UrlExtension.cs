namespace Common.Infrastructure
{
    using Be.Vlaanderen.Basisregisters.Api.Search.Helpers;

    public class UrlExtension
    {
        private readonly string _extension;

        public UrlExtension(string extension)
            => _extension = extension.IsNullOrWhiteSpace()
                ? string.Empty
                : $".{extension.TrimStart('.')}";

        public override string ToString() => _extension;
    }
}
