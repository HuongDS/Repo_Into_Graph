using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using Repo_Into_Graph_Application.Dtos.WorkflowAssessment;

namespace Repo_Into_Graph_Application.Mappings
{
    public class WorkflowProfile : Profile
    {
        public WorkflowProfile()
        {
            CreateMap<NodeDto, BusinessWorkflowNodeDto>()
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type.ToString()));

            CreateMap<EdgeDto, BusinessWorkflowEdgeDto>()
                .ForMember(dest => dest.Condition, opt => opt.MapFrom(src => src.Label));

            CreateMap<NodeDto, WorkflowNodeInputDto>();
            CreateMap<EdgeDto, WorkflowEdgeInputDto>();
        }
    }
}
