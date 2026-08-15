using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using Repo_Into_Graph_Application.Dtos.FewShot;
using Repo_Into_Graph_DataAccess.Models.FewShot;

namespace Repo_Into_Graph_Application.Mappings
{
    public class FewShotExampleProfile : Profile
    {
        public FewShotExampleProfile()
        {
            CreateMap<FewShotExample, FewShotExampleDto>();
            CreateMap<CreateFewShotExampleRequest, FewShotExample>();


        }
    }
}
