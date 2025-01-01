﻿using AutoMapper;
using Core.DTOs;
using Core.DTOs.Event;
using Core.Entities;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.Utilities.Constants;
using Core.Utilities.Results;
using System.Linq.Expressions;

namespace Infrastructure.Services
{
    public class EventService(
        IEventRepository _eventRepository,
        IMapper _mapper,
        IUserService _userService) : IEventService
    {
        #region Create
        public async Task<IResult> CreateEventAsync(CreateEventDto createEventDto, Guid userId, CancellationToken cancellationToken)
        {
             var isValidUser=await _userService.IsUserValidAsync(userId,cancellationToken);

            if (isValidUser == false)  
                return new ErrorResult(Messages.UserNotFound);

            if (!IsDateRangeValid(createEventDto.StartDate, createEventDto.EndDate))
            {
                return new ErrorResult(Messages.InvalidDateRange);
            }

            var eventEntity=_mapper.Map<Event>(createEventDto);
            if (eventEntity == null)  
                return new ErrorResult(Messages.CreateEventError); 
            
            eventEntity.OrganizerId = userId;
            
            var createResult = await _eventRepository.CreateAsync(eventEntity);
            return createResult>0
                ? new SuccessResult(Messages.CreateEventSuccess)
                : new ErrorResult(Messages.CreateEventError);
        }
        #endregion

        #region Delete
        public async Task<IResult> DeleteEventAsync(Guid eventId,Guid userId, CancellationToken cancellationToken)
        {
            var eventEntity=await _eventRepository.GetByIdAsync(eventId);
            
            if(eventEntity == null) 
                return new ErrorResult(Messages.EventNotFound);

            if (eventEntity.OrganizerId != userId)
                return new ErrorResult(Messages.UnauthorizedAccess);

            var deleteResult=await _eventRepository.DeleteAsync(eventId);
            return deleteResult > 0
                ? new SuccessResult(Messages.DeleteEventSuccess)
                : new ErrorResult(Messages.DeleteEventError);
        }
        #endregion

        #region Update
        public async Task<IResult> UpdateEventAsync(UpdateEventDto updateEventDto,Guid userId, CancellationToken cancellationToken)
        {
            var eventEntity=await _eventRepository.GetByIdAsync(updateEventDto.Id);
            if(eventEntity == null)
                return new ErrorResult(Messages.EventNotFound);

            if (eventEntity.OrganizerId != userId)
                return new ErrorResult(Messages.UnauthorizedAccess);

            CompleteUpdate(eventEntity, updateEventDto);
            if (!IsDateRangeValid(updateEventDto.StartDate, updateEventDto.EndDate))
            {
                return new ErrorResult(Messages.InvalidDateRange);
            }
            var updateResult = await _eventRepository.UpdateAsync(eventEntity);
            return updateResult > 0
                ? new SuccessResult(Messages.UpdateEventSuccess)
                : new ErrorResult(Messages.UpdateEventError);
        }
        #endregion


        #region Read
        public async Task<IDataResult<IEnumerable<ViewEventDto>>> GetAllEventsAsync(Expression<Func<Event, bool>> predicate, CancellationToken cancellationToken)
        {
            var eventList = await _eventRepository.GetAllAsync(predicate);
            return eventList is not null && eventList.Any()
               ? new SuccessDataResult<IEnumerable<ViewEventDto>>(_mapper.Map<IEnumerable<ViewEventDto>>(eventList))
               : new ErrorDataResult<IEnumerable<ViewEventDto>>(Messages.EmptyEventList);
        }

        public async Task<IDataResult<ViewEventDto>> GetEventByIdAsync(Guid eventId, CancellationToken cancellationToken)
        {
            var eventEntity= await _eventRepository.GetByIdAsync(eventId);
            return eventEntity is not null
                ? new SuccessDataResult<ViewEventDto> (_mapper.Map<ViewEventDto>(eventEntity))
                : new ErrorDataResult<ViewEventDto>(Messages.EventNotFound);
        }
        public async Task<IDataResult<PaginationDto<ViewEventDto>>> GetAllEventsWithPaginationAsync(
        int page, int pageSize, CancellationToken cancellationToken)
        {
            var eventsWithPagination = await _eventRepository.GetAllEventsWithPaginationAsync(page, pageSize);

            if (eventsWithPagination.Data.Any())
            {
                return new SuccessDataResult<PaginationDto<ViewEventDto>>(
                    new PaginationDto<ViewEventDto>
                    {
                        Data = _mapper.Map<IEnumerable<ViewEventDto>>(eventsWithPagination.Data),
                        CurrentPage = eventsWithPagination.CurrentPage,
                        TotalPages = eventsWithPagination.TotalPages,
                        PageSize = eventsWithPagination.PageSize,
                        TotalCount = eventsWithPagination.TotalCount
                    });
            }

            return new ErrorDataResult<PaginationDto<ViewEventDto>>(Messages.EmptyEventList);
        }

        public async Task<IDataResult<ViewEventWithParticipantsDto>> GetEventWithParticipantsAsync(Guid eventId, CancellationToken cancellationToken)
        {
            var eventEntity = await _eventRepository.GetEventWithParticipantsAsync(eventId);
            if (eventEntity is null)
                return new ErrorDataResult<ViewEventWithParticipantsDto>(Messages.EventNotFound);

            return !eventEntity.Participants.Any()
                ? new ErrorDataResult<ViewEventWithParticipantsDto>(Messages.EmptyParticipantList)
                : new SuccessDataResult<ViewEventWithParticipantsDto>(_mapper.Map<ViewEventWithParticipantsDto>(eventEntity));
        }
        public async Task<IDataResult<IEnumerable<ViewEventDto>>> GetEventListByDateRangeAsync( DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken)
        {
            if (!IsDateRangeValid(startDate, endDate))
            {
                return new ErrorDataResult<IEnumerable<ViewEventDto>>("Start date cannot be later than end date.");
            }


            var eventList = await _eventRepository.GetAllAsync(e => e.StartDate <= endDate && e.EndDate >= startDate);

            return eventList is not null && eventList.Any()
                   ? new SuccessDataResult<IEnumerable<ViewEventDto>>(_mapper.Map<IEnumerable<ViewEventDto>>(eventList), Messages.EventsRetrievedSuccessfully)
                   : new ErrorDataResult<IEnumerable<ViewEventDto>>(Messages.EmptyEventList);

        }
        public async Task<IDataResult<IEnumerable<ViewEventDto>>> GetOrganizedEventListForUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            var userExists=await _userService.IsUserValidAsync(userId, cancellationToken);
            if(userExists==false)
                return new ErrorDataResult<IEnumerable<ViewEventDto>>(Messages.UserNotFound);
           
            
            var eventList = await _eventRepository.GetAllAsync(e => e.OrganizerId == userId);
            return eventList is not null && eventList.Any()
               ? new SuccessDataResult<IEnumerable<ViewEventDto>>(_mapper.Map<IEnumerable<ViewEventDto>>(eventList))
               : new ErrorDataResult<IEnumerable<ViewEventDto>>(Messages.EmptyEventList);
        }
        public async Task<IDataResult<IEnumerable<ViewEventDto>>> GetParticipatedEventListForUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            var userExists = await _userService.IsUserValidAsync(userId, cancellationToken);
            if (userExists == false)
                return new ErrorDataResult<IEnumerable<ViewEventDto>>(Messages.UserNotFound);

            var eventList = await _eventRepository.GetAllAsync(e => e.Participants.Any(p => p.UserId == userId));
            return eventList is not null && eventList.Any()
               ? new SuccessDataResult<IEnumerable<ViewEventDto>>(_mapper.Map<IEnumerable<ViewEventDto>>(eventList))
               : new ErrorDataResult<IEnumerable<ViewEventDto>>(Messages.EmptyEventList);

        }
        public async Task<IDataResult<int>> GetParticipantCountForEventAsync(Guid eventId, CancellationToken cancellationToken)
        {
            var participantCount = await _eventRepository.GetParticipantCountAsync(eventId);

            return participantCount >= 0
         ? new SuccessDataResult<int>(participantCount,Messages.ParticipantCountRetrievedSuccessfully)
         : new ErrorDataResult<int>(Messages.EventNotFound);
        }

        #endregion


        #region Helper Methods 
        private static bool IsDateRangeValid(DateTimeOffset startDate, DateTimeOffset endDate)
        {
            return startDate <= endDate; 
        }
        private static void CompleteUpdate(Event eventEntity, UpdateEventDto updateEventDto)
        {
            eventEntity.EventName = updateEventDto.EventName;
            eventEntity.EventDescription = updateEventDto.EventDescription;
            eventEntity.StartDate = updateEventDto.StartDate;
            eventEntity.EndDate = updateEventDto.EndDate;
            eventEntity.Location = updateEventDto.Location;
            eventEntity.Timezone = updateEventDto.Timezone;
        }
       

        #endregion




    }
}
