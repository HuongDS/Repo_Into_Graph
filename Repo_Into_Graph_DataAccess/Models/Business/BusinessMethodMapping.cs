using Repo_Into_Graph_DataAccess.Models.Method;
using System;

namespace Repo_Into_Graph_DataAccess.Models.Business
{
    public class BusinessMethodMapping
    {
        public Guid Id { get; set; }
        public Guid BusinessId { get; set; }
        public Guid MethodSourceId { get; set; }
        public DateTime MappedAt { get; set; }
        public Business? Business { get; set; }
        public MethodSourceRecord? MethodSource { get; set; }
    }
}
