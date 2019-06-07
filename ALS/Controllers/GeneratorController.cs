﻿using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ALS.EntityСontext;
using Generator.MainGen;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ALS.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    //[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Student")]
    public class GeneratorController : Controller
    {
        private Gen _gen = new Gen();
        private ApplicationContext _db;

        public GeneratorController(ApplicationContext db)
        {
            _db = db;
        }

        // Подумать над вариантом - откуда брать
        [HttpPost]
        public async Task<IActionResult> GenNewTask(int lrId, int var)
        {
            var TemplateLaboratoryWorkId = _db.LaboratoryWorks.FirstOrDefault(lr => lr.LaboratoryWorkId == lrId).TemplateLaboratoryWorkId;
            if (TemplateLaboratoryWorkId == null) return BadRequest("TemplateLaboratoryWorkId is null");

            var tlwPath = _db.TemplateLaboratoryWorks.FirstOrDefault(twl => twl.TemplateLaboratoryWorkId == TemplateLaboratoryWorkId).TemplateTask;
            if (tlwPath == null) return BadRequest("TemplateLaboratoryWorkPath is null");

            try
            {
                var res = await _gen.Run(new Uri(tlwPath).AbsolutePath, lrId, var);
                if (res == null) return BadRequest("Result of generation is null");

                await _db.Variants.AddAsync(new Variant { LaboratoryWorkId = lrId, VariantNumber = var, Description = res.Template, LinkToModel = res.Code, InputDataRuns = res.Tests });
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }

            return Ok("New variant has created!");
        }
    }
}
