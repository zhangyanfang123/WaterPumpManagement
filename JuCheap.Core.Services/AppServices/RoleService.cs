﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using JuCheap.Core.Data;
using JuCheap.Core.Data.Entity;
using JuCheap.Core.Infrastructure;
using JuCheap.Core.Interfaces;
using JuCheap.Core.Models;
using JuCheap.Core.Models.Filters;
using JuCheap.Core.Infrastructure.Extentions;
using JuCheap.Core.Infrastructure.Utilities;
using Microsoft.EntityFrameworkCore;
using System;

namespace JuCheap.Core.Services.AppServices
{
    /// <summary>
    /// 角色契约实现
    /// </summary>
    public class RoleService : IRoleService
    {
        private readonly JuCheapContext _context;
        private readonly IMapper _mapper;

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="context"></param>
        /// <param name="mapper"></param>
        public RoleService(JuCheapContext context, IMapper mapper)
        {
            _mapper = mapper;
            _context = context;
        }

        /// <summary>
        /// 添加角色
        /// </summary>
        /// <param name="dto">角色模型</param>
        /// <returns></returns>
        public async Task<Guid> AddAsync(RoleDto dto)
        {
            var entity = _mapper.Map<RoleDto, RoleEntity>(dto);
            entity.Init();
            var dbSet = _context.Roles;
            dbSet.Add(entity);

            return await _context.SaveChangesAsync() > 0 ? entity.Id : Guid.Empty;
        }

        /// <summary>
        /// 更新角色
        /// </summary>
        /// <param name="dto">角色模型</param>
        /// <returns></returns>
        public async Task<bool> UpdateAsync(RoleDto dto)
        {
            var dbSet = _context.Roles;
            var entity = dbSet.FirstOrDefault(r => r.Id == dto.Id);
            _mapper.Map(dto, entity);
            return await _context.SaveChangesAsync() > 0;
        }

        /// <summary>
        /// 根据主键查询模型
        /// </summary>
        /// <param name="id">主键</param>
        /// <returns></returns>
        public async Task<RoleDto> FindAsync(Guid id)
        {
            var dbSet = _context.Roles;
            var entity = await dbSet.FirstOrDefaultAsync(r => r.Id == id);
            var dto = _mapper.Map<RoleEntity, RoleDto>(entity);
            return dto;
        }

        /// <summary>
        /// 批量逻辑删除
        /// </summary>
        /// <param name="ids">主键ID集合</param>
        /// <returns></returns>
        public async Task<bool> DeleteAsync(IEnumerable<Guid> ids)
        {
            var dbSet = _context.Roles;
            var entities = dbSet.Where(item => ids.Contains(item.Id));
            entities.ForEach(item => item.IsDeleted = true);
            return await _context.SaveChangesAsync() > 0;
        }

        /// <summary>
        /// 分页搜索
        /// </summary>
        /// <param name="filters">查询过滤参数</param>
        /// <returns></returns>
        public async Task<PagedResult<RoleDto>> SearchAsync(RoleFilters filters)
        {
            if (filters == null)
                return new PagedResult<RoleDto>(0, 0);

            var dbSet = _context.Roles;
            var query = dbSet.Where(item => !item.IsDeleted);

            if (filters.keywords.IsNotBlank())
                query = query.Where(item => item.Name.Contains(filters.keywords));

            if (filters.UserId.HasValue)
            {
                var userRoles = _context.UserRoles;
                var myRoleIds = userRoles.Where(item => item.UserId == filters.UserId.Value)
                                .Select(item => item.RoleId)
                                .ToList();
                query = filters.ExcludeMyRoles
                    ? query.Where(item => !myRoleIds.Contains(item.Id))
                    : query.Where(item => myRoleIds.Contains(item.Id));
            }

            return await query.OrderByDescending(item => item.CreateDateTime)
                .Select(item => new RoleDto
                {
                    Id = item.Id,
                    Name = item.Name,
                    Description = item.Description
                }).PagingAsync(filters.page, filters.rows);
        }

        /// <summary>
        /// 获取角色树
        /// </summary>
        /// <returns></returns>
        public async Task<List<TreeDto>> GetTreesAsync()
        {
            var list = await _context.Roles.Where(r => !r.IsDeleted).ToListAsync();
            return _mapper.Map<List<RoleEntity>, List<TreeDto>>(list);
        }

        /// <summary>
        /// 设置角色权限
        /// </summary>
        /// <returns></returns>
        public async Task<bool> SetRoleMenusAsync(List<RoleMenuDto> datas)
        {
            if (!datas.AnyOne()) return false;

            var roleId = datas.First().RoleId;
            var olds = await _context.RoleMenus.Where(item => item.RoleId == roleId).ToListAsync();
            var oldIds = olds.Select(item => item.MenuId);
            var newIds = datas.Select(item => item.MenuId);
            var adds = datas.Where(item => !oldIds.Contains(item.MenuId)).ToList();
            var removes = olds.Where(item => !newIds.Contains(item.MenuId)).ToList();
            if (adds.AnyOne())
            {
                var roleMenus = _mapper.Map<List<RoleMenuDto>, List<RoleMenuEntity>>(adds);
                roleMenus.ForEach(r => r.Init());
                _context.RoleMenus.AddRange(roleMenus);
            }
            if (removes.AnyOne())
            {
                _context.RoleMenus.RemoveRange(removes);
            }

            return await _context.SaveChangesAsync() > 0;
        }

        /// <summary>
        /// 清空该角色下的所有权限
        /// </summary>
        /// <param name="roleId">角色ID</param>
        /// <returns></returns>
        public async Task<bool> ClearRoleMenusAsync(Guid roleId)
        {
            if (roleId == Guid.Empty) return false;

            var list = await _context.RoleMenus.Where(item => item.RoleId == roleId).ToListAsync();
            _context.RoleMenus.RemoveRange(list);

            return await _context.SaveChangesAsync() > 0;
        }
    }
}
