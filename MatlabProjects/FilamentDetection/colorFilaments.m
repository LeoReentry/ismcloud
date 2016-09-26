function [img_colored] = colorFilaments(img,img_bin)

    % img=imresize(img,[516 692]);
    img_res=im2double(img);
    img_bin=im2double(img_bin);

    im_farbe=gray2rgb(img_res);

    [x,y]=find(img_bin==1);

    koord=zeros(length(x),2);
    koord(:,1)=x;
    koord(:,2)=y;

    for ii=1:length(x)

            im_farbe(koord(ii,1),koord(ii,2),1)=1;
            im_farbe(koord(ii,1),koord(ii,2),2)=0;   
            im_farbe(koord(ii,1),koord(ii,2),3)=0;       

    end

    img_name = strcat(char(java.util.UUID.randomUUID), '.jpg');
    imwrite(im_farbe, img_name);
    
    %img_colored = im_farbe;
    img_colored = img_name; % gib Name des Bildes zurück
end

