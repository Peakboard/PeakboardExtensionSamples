﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProGlove.Models
{
    public class AuthenticationResponse
    {
        public AuthenticationResult AuthenticationResult { get; set; }
        public ChallengeParameters ChallengeParameters { get; set; }
    }
}
