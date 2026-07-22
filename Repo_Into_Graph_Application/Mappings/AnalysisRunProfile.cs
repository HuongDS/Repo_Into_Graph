using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using Repo_Into_Graph_Application.Dtos.Analysis;
using Repo_Into_Graph_DataAccess.Models.Analysis;

namespace Repo_Into_Graph_Application.Mappings
{
    public class AnalysisRunProfile : Profile
    {
        public AnalysisRunProfile()
        {
            CreateMap<AnalysisRun, AnalysisRunDto>();
            CreateMap<CreateAnalysisRunRequest, AnalysisRun>()
                .ForMember(dest => dest.RepositoryPath, opt => opt.MapFrom(src => src.RepositoryPath.Trim()))
                .ForMember(dest => dest.RepoName, opt => opt.MapFrom(src => src.RepoName.Trim()))
                .ForMember(dest => dest.RepoOwner, opt => opt.MapFrom(src => src.RepoOwner.Trim()))
                .ForMember(dest => dest.RepoDescription, opt => opt.MapFrom(src => src.RepoDescription.Trim()))
                .ForMember(dest => dest.RepoUrl, opt => opt.MapFrom(src => src.RepoUrl.Trim()))
                .ForMember(dest => dest.RepoUpdatedAt, opt => opt.MapFrom(src => src.RepoUpdatedAt))
                .ForMember(dest => dest.RepoLanguage, opt => opt.MapFrom(src => src.RepoLanguage))
                .ForMember(dest => dest.RepoStars, opt => opt.MapFrom(src => src.RepoStars))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.IsPublic, opt => opt.MapFrom(src => src.IsPublic))
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => Guid.NewGuid()));

        }
    }
}
