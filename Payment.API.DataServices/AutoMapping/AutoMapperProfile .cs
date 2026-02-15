using System.Diagnostics.CodeAnalysis;
using AutoMapper;

namespace Security.DataServices.AutoMapping
{
    [ExcludeFromCodeCoverage]
    public class AutoMapperProfile : Profile
    {

        public AutoMapperProfile()
        {
            //CreateMap<User, CreateUserDto>()
            //    .ForMember(x => x.Id, opt => opt.MapFrom(x => x.Id))
            //    .ForMember(x => x.UserName, opt => opt.MapFrom(x => x.UserName));
                
            //CreateMap<User, ReturnUserDto>()
            //.ForMember(x => x.Id, opt => opt.MapFrom(x => x.Id))
            //.ForMember(x => x.UserName, opt => opt.MapFrom(x => x.UserName))
            //.ForMember(x => x.FullName, opt => opt.MapFrom(x => x.FullName))
            //.ForMember(x => x.Email, opt => opt.MapFrom(x => x.Email))
            //.ForMember(x => x.Language, opt => opt.MapFrom(x => x.Language))
            //.ForMember(x => x.MobileNumber, opt => opt.MapFrom(x => x.MobileNumber))
            //.ForMember(x => x.Culture, opt => opt.MapFrom(x => x.Culture));


            //CreateMap<Client, ClientDto>()
            //    .ForMember(x => x.ClientId, opt => opt.MapFrom(x => x.ClientId))
            //    .ForMember(x => x.ClientName, opt => opt.MapFrom(x => x.ClientName))
            //    .ForMember(x => x.IsActive, opt => opt.MapFrom(x => x.IsActive));
        }
    }
}
