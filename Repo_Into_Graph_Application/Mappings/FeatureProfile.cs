using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using Repo_Into_Graph_Application.Dtos.Feature;
using Repo_Into_Graph_DataAccess.Models.Feature;

namespace Repo_Into_Graph_Application.Mappings
{
    public class FeatureProfile : Profile
    {
        public FeatureProfile()
        {
            CreateMap<Feature, FeatureSummaryDto>()
                .ForMember(dest => dest.StepCount, opt => opt.MapFrom(src => src.Steps.Count));
            CreateMap<Feature, FeatureDetailDto>()
                .ForMember(dest => dest.Steps, opt => opt.MapFrom(src => src.Steps != null ? src.Steps.OrderBy(s => s.StepOrder) : Enumerable.Empty<FeatureStep>()));
            CreateMap<FeatureStep, FeatureStepDto>();
        }
    }
}
