using System;
using System.IO;
using System.Linq;
using System.Text;
using ALS.AntiPlagModule.Services;
using ALS.AntiPlagModule.Services.LexerFactory;
using ALS.AntiPlagModule.Services.LexerService;
using ALS.CheckModule.Compare.Checker;
using ALS.CheckModule.Compare.Finaliter;
using ALS.CheckModule.Compare.Preparer;
using ALS.CheckModule.Processes;
using ALS.EntityСontext;
using ALS.Services;
using ALS.Services.AuthService;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace ALS
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc()
                .AddNewtonsoftJson()
                .SetCompatibilityVersion(CompatibilityVersion.Version_3_0);
            services.AddEntityFrameworkNpgsql().AddDbContext<ApplicationContext>(options => options.UseNpgsql(Configuration.GetConnectionString("AlsDatabase")));
            
            services.AddSingleton<IAuthService>(new AuthService(Configuration));
            services.AddSingleton<ILexer>(new CppLexer(new CppLexerFactory()));
            services.AddDistributedMemoryCache();
            services.AddSession();
            services.AddHttpClient();
            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(cfg =>
                {
                    cfg.RequireHttpsMetadata = false;
                    cfg.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = Configuration["JwtIssuer"],
                        ValidateAudience = true,
                        ValidAudience = Configuration["JwtAudience"],
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration["JwtKey"])),
                        ClockSkew = TimeSpan.Zero
                    };
                });

            services.AddAuthorization(options =>
            {
                options.DefaultPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build();
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ApplicationContext dbContext)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHttpsRedirection();
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseSession();
            app.UseRouting();
            app.UseDefaultFiles();
            app.UseStaticFiles();
            
            //Custom cookie middleware
            app.Use(async (context, next) =>
            {
                var token = context.Session.GetString("Token");
                if (!String.IsNullOrEmpty(token))
                {
                    context.Request.Headers.Add("Authorization", "Bearer " + token);
                }
                await next();
            });
            
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllerRoute("default", "{controller}/{action=Index}/{id?}");
                    endpoints.MapRazorPages();
                });


            dbContext.Database.EnsureCreated();

            CreateNeedDirectory();
            SetupDatabase(dbContext);
            PreloadComponents();
        }
        /// <summary>
        /// Создание требуемых для приложения директорий если их нет в системе
        /// </summary>
        void CreateNeedDirectory()
        {
            var necessaryDirectories = new[] {"sourceCodeUser", "genExtensions", "uploads", "sourceCodeModel", "executeUser", "executeModel", "tmp", "modelTestingFiled"  };
            foreach (var item in necessaryDirectories)
            {
                if (!Directory.Exists(item))
                {
                    Directory.CreateDirectory(item);
                }
            }
        }
        
        void SetupDatabase(ApplicationContext context)
        {
            // check and add roles
            AuthService auth = new AuthService(Configuration);
            
            if (!context.Disciplines.Any())
            {
                context.Disciplines.Add(new Discipline { Name = "Программирование", Cipher = "pr1" });
                context.SaveChanges();
            }

            if (!context.Specialties.Any())
            {
                var softwareEngineering = new Specialty { Name = "Программная инженерия", Code = "09.03.04" };
                var computerScienceAndComputing = new Specialty
                    { Name = "Информатика и вычислительная техника", Code = "09.03.01" };
                context.Specialties.Add(softwareEngineering);
                context.Specialties.Add(computerScienceAndComputing);
                
                context.Groups.Add(new Group { Name = "ДИПР-31", Year = 2019, Specialty = softwareEngineering} );
                context.Groups.Add(new Group
                    { Name = "ДИНР-31", Year = 2019, Specialty = computerScienceAndComputing} );
                context.SaveChanges();
            }
            
            if (!context.Roles.Any())
            {
                context.Roles.Add(new Role { RoleId = 1, RoleName = RoleEnum.Student });
                context.Roles.Add(new Role { RoleId = 2, RoleName = RoleEnum.Teacher });
                context.Roles.Add(new Role { RoleId = 3, RoleName = RoleEnum.Admin });

                context.SaveChanges();
            }
            
            if (!context.Users.Any())
            {
                var student = new User { Surname = "Михайлов", Name = "Дмитрий", Patronymic = "Владимирович", Email = "tmpstudent@mail.com", PwHash = auth.GetHashedPassword("tmpstudent"), GroupId = 1};
                var student1 = new User { Surname = "Иванов", Name = "Иван", Patronymic = "Иванович", Email = "ivan@mail.com", PwHash = auth.GetHashedPassword("ivan"), GroupId = 2};
                var student2 = new User { Surname = "Петров", Name = "Петр", Patronymic = "Петрович", Email = "petr@mail.com", PwHash = auth.GetHashedPassword("petr"), GroupId = 1};
                var student3 = new User { Surname = "Макаров", Name = "Макар", Patronymic = "Макарович", Email = "makar@mail.com", PwHash = auth.GetHashedPassword("makar"), GroupId = 2};
                var student4 = new User { Surname = "Сидорова", Name = "Александра", Patronymic = "Михайловна", Email = "alexandrochka@mail.com", PwHash = auth.GetHashedPassword("alexandrochka"), GroupId = 1};
                var student5 = new User { Surname = "Владов", Name = "Владислав", Patronymic = "Владиславович", Email = "vlad@mail.com", PwHash = auth.GetHashedPassword("vlad"), GroupId = 2};
                var student6 = new User { Surname = "Федоров", Name = "Федор", Patronymic = "Федорович", Email = "fedor@mail.com", PwHash = auth.GetHashedPassword("fedor"), GroupId = 1};
                var teacher = new User { Surname = "Семёнов", Name = "Иван", Patronymic = "Васильевич", Email = "tmpprepod@mail.com", PwHash = auth.GetHashedPassword("tmpprepod") };
                var admin = new User { Surname = "Админов", Name = "Админ", Patronymic = "Админович", Email = "tmpadmin@mail.com", PwHash = auth.GetHashedPassword("tmpadmin") };

                
                context.Users.Add(student);
                context.Users.Add(student1);
                context.Users.Add(student2);
                context.Users.Add(student3);
                context.Users.Add(student4);
                context.Users.Add(student5);
                context.Users.Add(student6);
                context.Users.Add(admin);
                context.Users.Add(teacher);
                context.SaveChanges();

                context.UserRoles.Add(new UserRole { UserId = student.Id, RoleId = 1 });
                context.UserRoles.Add(new UserRole { UserId = student1.Id, RoleId = 1 });
                context.UserRoles.Add(new UserRole { UserId = student2.Id, RoleId = 1 });
                context.UserRoles.Add(new UserRole { UserId = student3.Id, RoleId = 1 });
                context.UserRoles.Add(new UserRole { UserId = student4.Id, RoleId = 1 });
                context.UserRoles.Add(new UserRole { UserId = student5.Id, RoleId = 1 });
                context.UserRoles.Add(new UserRole { UserId = student6.Id, RoleId = 1 });
                context.UserRoles.Add(new UserRole { UserId = teacher.Id, RoleId = 2 });
                context.UserRoles.Add(new UserRole { UserId = admin.Id, RoleId = 3 });
                
                context.SaveChanges();
            }
            


            if (!context.Themes.Any())
            {
                context.Themes.Add(new Theme { Name = "простые взаимодействие с числами", DisciplineCipher = "pr1"  });
                context.Themes.Add(new Theme { Name = "условные операторы", DisciplineCipher = "pr1" });
                context.SaveChanges();
            }
            if (!context.TemplateLaboratoryWorks.Any())
            {
                context.TemplateLaboratoryWorks.Add(new TemplateLaboratoryWork { TemplateTask = @"C:/Users/kampukter/source/repos/ALS/ALS.GeneratorModule/Tasks/task1.gentemp", ThemeId = 2 });
                //context.TemplateLaboratoryWorks.Add(new TemplateLaboratoryWork { TemplateTask = @"C:/Users/kampukter/source/repos/ALS/ALS.GeneratorModule/Docs/Exp3.txt", ThemeId = 1 });
                //context.TemplateLaboratoryWorks.Add(new TemplateLaboratoryWork { TemplateTask = @"C:/Users/kampukter/source/repos/ALS/ALS.GeneratorModule/Docs/Exp.txt", ThemeId = 2 });
                //context.TemplateLaboratoryWorks.Add(new TemplateLaboratoryWork { TemplateTask = @"C:/Users/kampukter/source/repos/ALS/ALS.GeneratorModule/Docs/Exp2.txt", ThemeId = 2 });
                context.SaveChanges();
            }

            if (!context.LaboratoryWorks.Any())
            {
                //context.LaboratoryWorks.Add(new LaboratoryWork { UserId = 9, ThemeId = 2, Name = "lr1", Description = "lr1_description", Constraints = "{\"Memory\": 4096000, \"Time\": 60000}", DisciplineCipher = "pr1"});
                context.LaboratoryWorks.Add(new LaboratoryWork { UserId = 9, ThemeId = 1, Name = "ЛР1 - работа с числами", Description = "Выполнить операции с числами", Constraints = "{\"Memory\": 4096000,\"Time\": 5000,\"Checker\" : \"AbsoluteChecker\"}", DisciplineCipher = "pr1", Evaluation=Evaluation.NotStrict});
                context.LaboratoryWorks.Add(new LaboratoryWork { TemplateLaboratoryWorkId = 1, UserId = 9, ThemeId = 2, Name = "ЛР2 - работа с файлами", Description = "Выполнить операции с числами из файла input.txt и запишите в файл output.txt", Constraints = "{\"Memory\": 4096000,\"Time\": 5000,\"Checker\" : \"FileChecker\", \"Finaliter\" : \"FileFinaliter\", \"Preparer\": \"FilePreparer\"}", DisciplineCipher = "pr1", Evaluation = Evaluation.Strict });
                context.LaboratoryWorks.Add(new LaboratoryWork
                {
                    TemplateLaboratoryWorkId = 1, UserId = 9, ThemeId = 2, Name = "ЛР3 - работа со строками",
                    Description =
                        "В стандартный поток ввода принимаются 10 строк, по одной выполнить для каждой строки выполнить операцию, указанную в варианте и вывести в стандартный поток вывода результирующи строки",
                    Constraints = "{\"Memory\": 4096000,\"Time\": 5000,\"Checker\" : \"AbsoluteChecker\"}",
                    DisciplineCipher = "pr1", Evaluation = Evaluation.Penalty
                });
                //context.LaboratoryWorks.Add(new LaboratoryWork { UserId = 2, ThemeId = 1, Name = "lr1", Description = "lr1_description", Constraints = "{\"Memory\": 4096000, \"Time\": 60000}", DisciplineCipher = "pr1"});
                //context.LaboratoryWorks.Add(new LaboratoryWork { UserId = 2, ThemeId = 1, Name = "lr2", Description = "Вывести четные элементы", Constraints = "{\"Memory\": 4096000, \"Time\": 60000}", DisciplineCipher = "pr1"});
                //context.LaboratoryWorks.Add(new LaboratoryWork { TemplateLaboratoryWorkId = 3, UserId = 2, ThemeId = 2, Name = "lr3", Description = "descrition", Constraints = "{\"Memory\": 4096000, \"Time\": 60000}", DisciplineCipher = "pr1" });
                context.SaveChanges();
            }
            if (!context.Variants.Any())
            {
                context.Variants.Add(new Variant { VariantNumber = 1, LaboratoryWorkId = 1, Description = "var descr" });
                context.Variants.Add(new Variant { VariantNumber = 2, LaboratoryWorkId = 1, Description = "Тест" });
                context.Variants.Add(new Variant { VariantNumber = 1, LaboratoryWorkId = 3, LinkToModel = Path.Combine(Environment.CurrentDirectory, "executeModel", $"{ProcessCompiler.CreatePath(2, 1)}.exe"), Description = "В стандартный поток ввода принимаются 10 целых чисел. Вывести в стандартный поток вывода только чётные числа.", InputDataRuns = "\"тест1[60] : 2|4|6|8|6|10|12|14|18|20;тест2 [20]: 2|4|6|8|6|10|12|14|18|20;тест3 [20]: 2|4|6|8|6|10|12|14|18|20;тест4 [20]: 2|4|6|8|6|10|12|14|18|20;тест5 [20]: 1|2|3|4|5|6|7|8|9|10;\""});
                context.Variants.Add(new Variant { VariantNumber = 1, LaboratoryWorkId = 2, LinkToModel = Path.Combine(Environment.CurrentDirectory, "executeModel", $"{ProcessCompiler.CreatePath(1, 3)}.exe"), Description = "Прочитать числа из одного файла и записать квадраты этих чисел в другой. Считывать символы до встречи буквы 'n'", InputDataRuns = "\"тест1[60] : #случайноеЦелое(10 , 200, 10);тест2 [20]: 2|4|6|8|6|10|12|14|18|20;тест3 [20]: 1|2|3|4|5|6|7|8|9|10;\"" });
                context.Variants.Add(new Variant { VariantNumber = 3, LaboratoryWorkId = 1, LinkToModel = Path.Combine(Environment.CurrentDirectory, "executeModel", $"{ProcessCompiler.CreatePath(3, 1)}.exe"), Description = "Все символы в строке расположить в обратном порядке", InputDataRuns = "\"тест1[20] : asddsaasd|Hello|qwerrty|asdfdgfdgh|zxcbvcbv|asdasdasd|klkjkj|yhyhyh|opopopo|jkjkjkk;тест2 [5]: Jow|woJ|Raw|waR|Kill|Llik|ClikKK|Sam|Zxc|Cxz;тест3 [75]: Hi|Di|Ui|ds|sD|as|fd|qw|re|sd; \"" });
                context.SaveChanges();
            }

            if (!context.AssignedVariants.Any())
            {
                context.AssignedVariants.Add(new AssignedVariant {UserId = 3, VariantId = 2, AssignDateTime = DateTime.Now});
                context.AssignedVariants.Add(new AssignedVariant {UserId = 2, VariantId = 2, AssignDateTime = DateTime.Now});
                context.AssignedVariants.Add(new AssignedVariant {UserId = 1, VariantId = 2, AssignDateTime = DateTime.Now});
                context.AssignedVariants.Add(new AssignedVariant {UserId = 1, VariantId = 1, AssignDateTime = DateTime.Now});
                
                context.AssignedVariants.Add(new AssignedVariant {UserId = 4, VariantId = 3, AssignDateTime = DateTime.Now});
                context.AssignedVariants.Add(new AssignedVariant {UserId = 4, VariantId = 2, AssignDateTime = DateTime.Now});
                context.AssignedVariants.Add(new AssignedVariant {UserId = 4, VariantId = 1, AssignDateTime = DateTime.Now});
                
                context.AssignedVariants.Add(new AssignedVariant {UserId = 5, VariantId = 1, AssignDateTime = DateTime.Now});
                context.AssignedVariants.Add(new AssignedVariant {UserId = 5, VariantId = 2, AssignDateTime = DateTime.Now});

                context.AssignedVariants.Add(new AssignedVariant {UserId = 6, VariantId = 1, AssignDateTime = DateTime.Now});
                context.AssignedVariants.Add(new AssignedVariant {UserId = 6, VariantId = 2, AssignDateTime = DateTime.Now});
                
                context.SaveChanges();
            }
            
            if (!context.Solutions.Any())
            {
                context.Solutions.Add(new Solution { IsSolved = false, SendDate = DateTime.Now.AddHours(20), IsCompile = true, AssignedVariantId = 4});
                
                
                context.Solutions.Add(new Solution { IsSolved = false, IsCompile = true, SendDate = DateTime.Now.AddHours(2), AssignedVariantId = 5});
                context.Solutions.Add(new Solution { IsSolved = false, IsCompile = false, SendDate = DateTime.Now.AddHours(12), AssignedVariantId = 5});
                
                context.Solutions.Add(new Solution { IsSolved = false, IsCompile = true, SendDate = DateTime.Now.AddHours(10), AssignedVariantId = 6});
                context.Solutions.Add(new Solution { IsSolved = false, IsCompile = false, SendDate = DateTime.Now.AddHours(9), AssignedVariantId = 6});
                context.Solutions.Add(new Solution { IsSolved = false, IsCompile = true, SendDate = DateTime.Now.AddHours(5), AssignedVariantId = 7});
                context.Solutions.Add(new Solution { IsSolved = false, IsCompile = false, SendDate = DateTime.Now.AddHours(17), AssignedVariantId = 7});
                
                context.Solutions.Add(new Solution { IsSolved = true, IsCompile = true, SendDate = DateTime.Now.AddHours(15), AssignedVariantId = 8});
                context.Solutions.Add(new Solution { IsSolved = false, IsCompile = false,SendDate = DateTime.Now.AddHours(6), AssignedVariantId = 9});
                
                context.SaveChanges();
            }

            if (!context.Plans.Any())
            {
                context.Plans.Add(new Plan {DisciplineCipher = "pr1", GroupId = 1, UserId = 9 });
                context.Plans.Add(new Plan {DisciplineCipher = "pr1", GroupId = 2, UserId = 9 });
                
                context.SaveChanges();
            }
        }
        /// <summary>
        /// Метод для первичной сборки
        /// </summary>
        private async void PreloadComponents()
        {
            await new CheckerList().ReloadActions();
            await new PreparerList().ReloadActions();
            await new FinaliterList().ReloadActions();
        }
    }
}