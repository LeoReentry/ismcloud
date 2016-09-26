function [TEFL, real_length, img_colored] = new_detectFilaments(img_file, var_thresh, dist_thresh, RG_thresh, fill_area, dilate_value)
% Definition of parameters values % Now they are set as input param --arc
% var_thresh = 0.0025; % variance threshold
% dist_thresh = 8.5; % distance-map threshold
% RG_thresh = 3.75; % R.R.G. threshold
% fill_area = 4; % Restricted filling threshold
% dilate_value = 16; % Size of square SE used for dilation of dist.-map mask

%% read Image File
%
img_orig = imread(img_file);
%img_orig = img_file; % input param is the image, not the path --arc

%% Resizes image

img_orig = imresize(img_orig,0.5);
%% Low Pass Filter - Gaussian ([3,3], 0.9)
h = fspecial('gaussian',[3 3],0.9);
img = imfilter(img_orig,h);

clear h

%% Borders Enhancement - Adds Top-Hat and removes Bottom-Hat 

% se = strel('diamond',3); % structure element - diamond with Radius = 3
se = [0     0     0     1     0     0     0;...
      0     0     1     1     1     0     0;...
      0     1     1     1     1     1     0;...
      1     1     1     1     1     1     1;...
      0     1     1     1     1     1     0;...
      0     0     1     1     1     0     0;...
      0     0     0     1     0     0     0];


img = imsubtract(imadd(img,imtophat(img,se)), imbothat(img,se));
clear se

%% Variance

img_var = single( (stdfilt(img)).^2 ); % calculates variance
img_var = single( img_var./max(max(img_var)) ); % normalization 

img_var = imcrop(img_var, [3 3 691 515]); % removes borders, compromised 
                                          % during variance calculation
img_bw = img_var > var_thresh; % applies variance threshold

clear img img_var 

% Size Filter

img_open = bwareaopen(img_bw, 200); % removes objects composed by 
                                    % less than 200pixels

clear img_bw

% Filling
img_fill = imfill(img_open,'holes');

% Restrict Filling process
img_sub = bitxor(img_fill,img_open); % detects filled holes
dist_fill = bwdist(~img_sub); % calculates distance map of filled holes

% only holes below the threshold value remain filled
filt_dist = bitand(dist_fill < fill_area,dist_fill > 0); 
img_fill = bitor(img_open,filt_dist);

clear img_sub dist_fill filt_dist

% Mask with Bulky Structures - Distance Map to black background
img_dist = bwdist(~img_fill);
img_dist = img_dist > dist_thresh; % applies dist.-map threshold to create 
                                   % a mask with bulky structures

se = strel('square',dilate_value); 
dist_mask = ~imdilate(img_dist,se); % dilation of the mask 

img_fill = bitand(img_fill,dist_mask); % mask is applied

% Size Filter
img_fill = (bwareaopen(img_fill, 200)); % removes objects composed by 
                                        % less than 200pixels

clear img_dist dist_mask

% Skeleton
img_thin = bwmorph(img_fill,'skel',inf);
clear img_fill

%% Isolate Skeleton's Spine
[L,N] = bwlabel(img_thin,8); % isolates each object present in image

obj_pixelsList = regionprops(L,'PixelIdxList'); % list of white pixels 
                                                % composing each obj.
obj_Images = regionprops(L,'Image');% portraits containing each single obj.

dim_imgs = size(img_thin); % dimensions X and Y of the image 
new_img = zeros(dim_imgs); % new image containing processed objects
lengths = zeros(N,1); % length of each spine (distance of pixels)

for i = 1:N   % for each object  
    
    endpoints = bwmorph(obj_Images(i,1).Image, 'endpoints'); % list of endpoints
    [y,x] = find(endpoints); % coordinates (x,y) of each endpoint
    
    % detection of the four suitable endpoints:       
    difX = abs(x-size(obj_Images(i,1).Image,2)); % dist. between endpoints and X limit
    difY = abs(y-size(obj_Images(i,1).Image,1)); % dist. between endpoints and Y limit
    
    [~,list_idx(1)] = max(difX(:)); % closest to the left border
    [~,list_idx(2)] = min(difX(:)); % closest to the right border
    [~,list_idx(3)] = max(difY(:)); % closest to the top
    [~,list_idx(4)] = min(difY(:)); % closest to the bottom
    
    % vector containing: 
    % maximal distance between endpoints | starting endpoint | farthest (final) endpoint 
    maxD = [0 0 0];
    
    for endp = 1:4
        D = bwdistgeodesic(obj_Images(i,1).Image, x(list_idx(endp)),y(list_idx(endp)),'quasi-euclidean');
        D(isinf(D)) = 0; % attributes 0 to pixels containing infinite value
        D(isnan(D)) = 0; % attributes 0 to pixels containing non-available value
        
        if maxD(1) < max(D(:)) % updates maximal distance between endpoints
           maxD = [max(D(:)) list_idx(endp) find(D == max(D(:)),1)]; 
           copyD = D; % retains a copy of the geodesic transform related to 
                      % the endpoint that is the starting point for this maximal distance
        end
        
    end     

    [Y,X] = ind2sub(size(D),maxD(3)); % coordinates (x,y) of the farthest endpoint
    
    % geodesic transform starting from this farthest endpoint
    D2 = bwdistgeodesic(obj_Images(i,1).Image, double(X),double(Y), 'quasi-euclidean');
    D = copyD + D2; % addition of distances calculated in both directions
    D = round(D * 8) / 8; % rounding values

    lengths(i,1) = min(D(:)); % update vector containing length of each spine
    
    D(isnan(D)) = inf;
    paths = imregionalmin(D); % detection of shortest path based on region minima
    
    % maps each object from its portrait back to the original image
    [objY,objX] = ind2sub(dim_imgs,obj_pixelsList(i,1).PixelIdxList(1,1));
    [imY,imX] = find(obj_Images(i,1).Image > 0,1);
    [pY,pX] = find(paths > 0);     
    
    offsetX = objX-imX; offsetY = objY-imY; 
    
    id_white = sub2ind(dim_imgs,pY+offsetY,pX+offsetX);
    new_img(id_white) = 1;
end

clear D copyD D2 paths pY offsetY pX offsetX id_white list_idx y x imY imX
%% Analysis of remaining objects - Reduced radius of gyration
[L,N] = bwlabel(new_img,8); % isolates each object present in image

obj_centroids = regionprops(L, 'Centroid'); % centroids of each object
obj_diameters = regionprops(L, 'EquivDiameter'); % equiv.diameter of " "
obj_listPixels = regionprops(L,'PixelIdxList'); % list of white pixels 
                                                % composing each obj.
RG = zeros(N,1); %vector containing values R.R.G. for each obj.
map_rg = L;

for i = 1:N    
    
    % coordinates (x,y) of each pixel composing the object
    [pixels_y,pixels_x] = ind2sub(dim_imgs,obj_listPixels(i).PixelIdxList);   
    
    % computes distance between each pixel and the obj.'s centroid
    sum_x = sum( (pixels_x - obj_centroids(i,1).Centroid(1,1)).^2 );
    sum_y = sum( (pixels_y - obj_centroids(i,1).Centroid(1,2)).^2 );

    % computes the moments in each axis
    M2x = sum_x/length(pixels_x);
    M2y = sum_y/length(pixels_y);
    
    % computes the R.R.G.
    RG(i) = sqrt(M2x + M2y) / (obj_diameters(i).EquivDiameter ./2); 
    
    % applies R.R.G. threshold
    if RG(i) > RG_thresh
        map_rg(obj_listPixels(i).PixelIdxList) = 1;          
    else
        map_rg(obj_listPixels(i).PixelIdxList) = 0;
        lengths(i) = 0;
    end   
end



% save colored image as bitmap --arc
% todo: return the colored image as output param and create bitmap with it in c# instead of saving it.
img_colored = colorFilaments(img_orig, map_rg);


clear pixels_x pixels_y sum_x sum_y M2x M2y RG obj_centroids obj_diameters 
clear obj_listPixels i L N
%% Calculate Histogram
lengths(lengths == 0) = [];
real_length = lengths*12.9/40; % applies scale factor to the lengths values
TEFL = sum(real_length); % computes the total extend length of filaments 
                          % present in the whole image

% returns the file's name, TEFL-Img. and length of each object
% str_return = struct('name',img_file,'total',total,'objs',real_length);
