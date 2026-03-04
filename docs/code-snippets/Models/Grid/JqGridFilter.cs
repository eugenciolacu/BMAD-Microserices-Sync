namespace ServerService.Models.Grid
{
    public class JqGridFilter
    {
        public string GroupOp { get; set; } = "AND";
        public List<JqGridFilterRule> Rules { get; set; } = new();
    }

    public class JqGridFilterRule
    {
        public string Field { get; set; } = string.Empty;
        public string Op { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty;
    }
}