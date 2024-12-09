﻿using Core.Utilities.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Interfaces.Services
{
    public interface IParticipantService
    {
        Task<IResult> SendInvitationAsync(int organizerId, int eventId, List<int> userIds);

    }
}
