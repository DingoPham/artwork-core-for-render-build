﻿using ArtworkCore.Class;
using ArtworkCore.Models;
using ArtworkCore.Services.DBconnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Npgsql;
using System.Data;
using System.Text;
using ArtworkCore.Services;
using ArtworkCore.FilterAttribute;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace ArtworkCore.Controllers
{
    [ApiController]
    [ServiceFilter(typeof(CustomFilter))]
    [Authorize]
    [Route("[controller]")]
    public class NSFWVideoController : Controller
    {
        private readonly ILogger<NSFWVideoController> _logger;
        private IConfiguration _configuration;
        private IPostgresSQL_Connection _db_action;
        private readonly AgeCaculator _ageCaculator;
        public NSFWVideoController(ILogger<NSFWVideoController> logger, IConfiguration configuration, IPostgresSQL_Connection db_action, AgeCaculator ageCaculator)
        {
            _db_action = db_action;
            _configuration = configuration;
            _logger = logger;
            _ageCaculator = ageCaculator;
        }

        #region Get
        [HttpGet]
        [Authorize(Roles = "admin,user")]
        public IActionResult Get()
        {
            string encodedUsername = User.Claims.FirstOrDefault(_ => _.Type == "username")?.Value;
            string username = Encoding.UTF8.GetString(Convert.FromBase64String(encodedUsername));

            DateTime userAge = DateTime.MinValue;
            try
            {
                using (NpgsqlConnection _connect = _db_action.Connection())
                {
                    _connect.Open();

                    string query = "SELECT age FROM master.account WHERE username = :Username";
                    using (NpgsqlCommand cmd = new(query, _connect))
                    {
                        cmd.Parameters.Add(new NpgsqlParameter("Username", NpgsqlTypes.NpgsqlDbType.Text)
                        {
                            Value = username
                        });
                        using (NpgsqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                userAge = reader.GetDateTime(0);
                            }
                            else
                            {
                                return Unauthorized(new { authMessage = "User not found!" });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching user age");
                return StatusCode(500, "Internal server error");
            }

            int userBirth = _ageCaculator.CalculateAge(userAge);
            // Check if the user is at least 18 years old
            if (userBirth < 18)
            {
                return BadRequest(new { ageMessage = "You must be at least 18 years old to access this content!" });
            }

            DataTable dt = new();
            List<NsfwVideo> list_data_nsfwvideo = new();

            try
            {
                using (NpgsqlConnection _connect = _db_action.Connection())
                {
                    _connect.Open();

                    string query = $"SELECT * FROM master.nsfwvideo ORDER BY \"order\" ASC;";
                    dt = new DataTable();
                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, _connect))
                    {
                        NpgsqlDataReader dataReader = cmd.ExecuteReader();
                        dt.Load(dataReader);
                    }
                    list_data_nsfwvideo = (from rw in dt.AsEnumerable()
                                       select new NsfwVideo()
                                       {
                                           Id = Convert.ToString(rw["id"]),
                                           NsfwVideoUrl = Convert.ToString(rw["nsfw_video_url"]),
                                           NsfwVideoName = Convert.ToString(rw["nsfw_video_name"]),
                                           NsfwVideoDescribe = Convert.ToString(rw["nsfw_video_describe"]),
                                           Order = Convert.ToInt32(rw["order"])
                                       }).ToList();
                }
                var list_total = new
                {
                    list_data_nsfwvideo = list_data_nsfwvideo
                };
                return Ok(list_total);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching NSFW art");
                return StatusCode(500, "Internal server error");
            }
        }
        #endregion

        #region Post
        [HttpPost("post")]
        [Authorize(Roles = "admin")]
        public IActionResult Post(PostRequest request)
        {
            string newImageId = Guid.NewGuid().ToString();
            string message = string.Empty;
            DataTable dt = new();
            List<NpgsqlParameter> list_param = new();
            NpgsqlConnection _connect = _db_action.Connection();

            try
            {
                _connect.Open();

                switch (request.Type.ToLower())
                {
                    case "nsfwvideo":
                        var nsfwvideo = JsonConvert.DeserializeObject<NsfwVideo>(request.Data.ToString());
                        if (nsfwvideo == null)
                            return BadRequest("Invalid data");

                        nsfwvideo.Id = newImageId;

                        list_param.Add(_db_action.ParamMaker("id", nsfwvideo.Id, DbType.String));
                        list_param.Add(_db_action.ParamMaker("nsfw_video_url", nsfwvideo.NsfwVideoUrl, DbType.String));
                        list_param.Add(_db_action.ParamMaker("nsfw_video_name", nsfwvideo.NsfwVideoName, DbType.String));
                        list_param.Add(_db_action.ParamMaker("nsfw_video_describe", nsfwvideo.NsfwVideoDescribe, DbType.String));

                        string nsfw_video_query = $"INSERT INTO master.nsfwvideo (id, nsfw_video_url, nsfw_video_name, nsfw_video_describe)VALUES(:id, :nsfw_video_url, :nsfw_video_name, :nsfw_video_describe);";
                        using (NpgsqlCommand cmd = new NpgsqlCommand(nsfw_video_query, _connect))
                        {
                            foreach (NpgsqlParameter param in list_param)
                            {
                                cmd.Parameters.Add(param);
                            }
                            cmd.ExecuteNonQuery();
                        }

                        message = "Insert video successfully";
                        break;

                    default:
                        return BadRequest("Unknow type");
                }

                _connect.Close();
            }
            catch (Exception ex)
            {
                message = "Insert video failed\n\r" + ex;
                return StatusCode(500, new { message });
            }
            return Ok(new { message, id = newImageId });
        }
        #endregion

        #region Put
        [HttpPut("put/{id}")]
        [Authorize(Roles = "admin")]
        public IActionResult Put(string id, PutRequest request)
        {
            string message = string.Empty;
            DataTable dt = new();
            List<NpgsqlParameter> list_param = new();
            NpgsqlConnection _connect = _db_action.Connection();

            try
            {
                _connect.Open();
                switch (request.Type.ToLower())
                {
                    case "nsfwvideo":
                        var nsfwvideo = JsonConvert.DeserializeObject<NsfwVideo>(request.Data.ToString());
                        list_param.Add(_db_action.ParamMaker("id", id, DbType.String));
                        list_param.Add(_db_action.ParamMaker("nsfw_video_url", nsfwvideo.NsfwVideoUrl, DbType.String));
                        list_param.Add(_db_action.ParamMaker("nsfw_video_name", nsfwvideo.NsfwVideoName, DbType.String));
                        list_param.Add(_db_action.ParamMaker("nsfw_video_describe", nsfwvideo.NsfwVideoDescribe, DbType.String));

                        string nsfw_video_query = $"UPDATE master.nsfwvideo SET nsfw_video_url = :nsfw_video_url, nsfw_video_name = :nsfw_video_name, nsfw_video_describe = :nsfw_video_describe WHERE id = :id;";
                        using (NpgsqlCommand cmd = new NpgsqlCommand(nsfw_video_query, _connect))
                        {
                            foreach (NpgsqlParameter param in list_param)
                            {
                                cmd.Parameters.Add(param);
                            }
                            cmd.ExecuteNonQuery();
                        }

                        message = "Update video successfully";
                        break;
                }

                _connect.Close();
            }
            catch (Exception ex)
            {
                message = "Update video failed\n\r" + ex;
                return StatusCode(500, new { message });
            }

            return Ok(new { message });
        }
        #endregion

        #region Delete
        [HttpDelete("del")]
        [Authorize(Roles = "admin")]
        public IActionResult Delete(DeleteRequest request)
        {
            string message = string.Empty;
            DataTable dt = new();
            List<NpgsqlParameter> list_param = new();
            NpgsqlConnection _connect = _db_action.Connection();

            try
            {
                _connect.Open();
                switch (request.Type.ToLower())
                {
                    case "nsfwvideo":
                        var nsfwvideo = JsonConvert.DeserializeObject<NsfwVideo>(request.Data.ToString());
                        list_param.Add(_db_action.ParamMaker("id", nsfwvideo.Id, DbType.String));

                        string nsfw_video_query = $"DELETE FROM master.nsfwvideo WHERE id = :id;";
                        using (NpgsqlCommand cmd = new NpgsqlCommand(nsfw_video_query, _connect))
                        {
                            foreach (NpgsqlParameter param in list_param)
                            {
                                cmd.Parameters.Add(param);
                            }
                            cmd.ExecuteNonQuery();
                        }

                        message = "Delete video successfully";
                        break;
                }

                _connect.Close();
            }
            catch (Exception ex)
            {
                message = "Delete video failed\n\r" + ex;
            }

            return Ok(message);
        }
        #endregion

        #region Order
        [HttpPut("order")]
        [Authorize(Roles = "admin")]
        public IActionResult Order([FromBody] NsfwVideoOrderRequest request)
        {
            string message = string.Empty;
            NpgsqlConnection _connect = _db_action.Connection();

            try
            {
                _connect.Open();

                if (request.NsfwVideos == null || !request.NsfwVideos.Any())
                {
                    return BadRequest("Danh sách ảnh không hợp lệ");
                }

                // Cập nhật thứ tự cho từng ảnh trong danh sách
                using (NpgsqlCommand cmd = new NpgsqlCommand())
                {
                    cmd.Connection = _connect;
                    cmd.CommandType = CommandType.Text;

                    // Chuẩn bị câu lệnh SQL để cập nhật từng bản ghi
                    StringBuilder queryBuilder = new StringBuilder();
                    List<NpgsqlParameter> parameters = new List<NpgsqlParameter>();

                    for (int i = 0; i < request.NsfwVideos.Count; i++)
                    {
                        var nsfwvideo = request.NsfwVideos[i];
                        queryBuilder.Append($"UPDATE master.nsfwvideo SET \"order\" = @order{i} WHERE id = @id{i};");
                        parameters.Add(new NpgsqlParameter($"@order{i}", i));
                        parameters.Add(new NpgsqlParameter($"@id{i}", nsfwvideo.Id));
                    }

                    cmd.CommandText = queryBuilder.ToString();
                    cmd.Parameters.AddRange(parameters.ToArray());
                    cmd.ExecuteNonQuery();
                }

                message = "Cập nhật thứ tự video thành công";
                _connect.Close();
            }
            catch (Exception ex)
            {
                message = "Cập nhật thứ tự video thất bại\n\r" + ex.Message;
                return StatusCode(500, new { message });
            }
            finally
            {
                _connect.Dispose();
            }

            return Ok(new { message });
        }
        #endregion 
    }
}
