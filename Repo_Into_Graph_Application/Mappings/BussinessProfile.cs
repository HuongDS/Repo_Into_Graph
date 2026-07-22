using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using Repo_Into_Graph_Application.Dtos.Business;
using Repo_Into_Graph_DataAccess.Models.Business;

namespace Repo_Into_Graph_Application.Mappings
{
    public class BussinessProfile : Profile
    {
        protected BussinessProfile()
        {
            CreateMap<Bussiness, BusinessViewDto>();
        }
    }
}
