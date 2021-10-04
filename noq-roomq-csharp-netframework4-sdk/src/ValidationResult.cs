namespace NoQ.RoomQ
{
    public class ValidationResult
    {
        private string redirectURL = null;

        public ValidationResult(string redirectURL)
        {
            this.redirectURL = redirectURL;
        }

        public bool NeedRedirect()
        {
            return this.redirectURL != null;
        }

        public string GetRedirectURL()
        {
            return this.redirectURL;
        }
    }
}
