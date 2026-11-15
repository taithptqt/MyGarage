using AutoMapper;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class ServiceStaffAssignmentService : GenericService<ServiceStaffAssignment>, IServiceStaffAssignmentService
    {
        private readonly IMapper _mapper;

        public ServiceStaffAssignmentService(IServiceStaffAssignmentRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public List<ServiceStaffAssignmentResponseDto> MapToDtos(IEnumerable<ServiceStaffAssignment> items)
        {
            return _mapper.Map<List<ServiceStaffAssignmentResponseDto>>(items);
        }

        public ServiceStaffAssignmentResponseDto MapToDto(ServiceStaffAssignment item)
        {
            return _mapper.Map<ServiceStaffAssignmentResponseDto>(item);
        }
    }
}
