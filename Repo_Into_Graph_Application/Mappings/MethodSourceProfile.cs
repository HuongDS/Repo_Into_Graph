using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using Repo_Into_Graph_Application.Dtos.Method;
using Repo_Into_Graph_DataAccess.Models.Method;

namespace Repo_Into_Graph_Application.Mappings
{
    public class MethodSourceProfile : Profile
    {
        public MethodSourceProfile()
        {
            CreateMap<MethodSourceRecord, MethodSourceDto>();
        }
    }
}
