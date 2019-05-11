﻿using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ALS.DTO;
using ALS.EntityСontext;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;

namespace ALS.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly SignInManager<User> _signInManager;
        private readonly UserManager<User> _userManager;
        private readonly IConfiguration _configuration;

        public UsersController(SignInManager<User> signInManager, UserManager<User> userManager, IConfiguration configuration)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _configuration = configuration;
        }

        [HttpPost]
        public async Task Login([FromBody] UserLoginDTO model)
        {
            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, false, false);

            if (result.Succeeded)
            {
                var appUser = _userManager.Users.SingleOrDefault(r => r.Email == model.Email);
                await SendIdentityResponse(model.Email, appUser);

            }
            else
            {
                Response.StatusCode = 400;
                await Response.WriteAsync("Invalid mail or password.");
                return;
            }
        }

        /// <summary>
        /// Send response when user successfully register/login
        /// </summary>
        /// <param name="Email">email from request</param>
        /// <param name="appUser">user</param>
        /// <returns></returns>
        private async Task SendIdentityResponse(string Email, User appUser)
        {
            var response = new
            {
                //access_token = GenerateJWT(Email, appUser),
                email = appUser.Email,
                id = appUser.Id
            };

            // сериализация ответа
            Response.StatusCode = 200;
            Response.ContentType = "application/json";
            await Response.WriteAsync(JsonConvert.SerializeObject(response, new JsonSerializerSettings { Formatting = Formatting.Indented }));
        }

        [HttpPost]
        public async Task Register([FromBody] UserRegisterDTO model)
        {
        }
        
    }
}